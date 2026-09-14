using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainingPlatform.Catalog;
using TrainingPlatform.TenantData;

namespace TrainingPlatform.Notifications;

// ---- Envío de correo (SMTP, sin dependencias externas) ----
public record EmailAttachment(string FileName, byte[] Content, string ContentType);

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null);
}

// Usa el relay SMTP configurado en Email:* (p. ej. SendGrid, Office365).
// Si Email:Host no está configurado, no hace nada (la app corre igual).
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    public SmtpEmailSender(IConfiguration cfg) { _cfg = cfg; }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null)
    {
        var host = _cfg["Email:Host"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(toEmail)) return;

        var fromEmail = _cfg["Email:From"] ?? "no-reply@local";
        var fromName = _cfg["Email:FromName"] ?? "Training Platform";
        var port = int.TryParse(_cfg["Email:Port"], out var p) ? p : 587;
        var ssl = !bool.TryParse(_cfg["Email:UseSsl"], out var s) || s; // default true

        using var msg = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(new MailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? toEmail : toName));

        // Los adjuntos se copian a un MemoryStream propio porque MailMessage los
        // lee al enviar, no al añadirlos.
        var flujos = new List<MemoryStream>();
        foreach (var a in attachments ?? Enumerable.Empty<EmailAttachment>())
        {
            var ms = new MemoryStream(a.Content, writable: false);
            flujos.Add(ms);
            msg.Attachments.Add(new Attachment(ms, a.FileName, a.ContentType));
        }

        using var client = new SmtpClient(host, port) { EnableSsl = ssl };
        var user = _cfg["Email:User"];
        var pass = _cfg["Email:Password"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, pass);

        try { await client.SendMailAsync(msg); }
        finally { foreach (var f in flujos) f.Dispose(); }
    }
}

// Envío vía la API HTTP de Brevo (v3), en vez de SMTP: un solo secreto (Email:ApiKey,
// cabecera "api-key"), sin necesidad de generar una SMTP key ni depender de un puerto
// SMTP abierto en el servidor. Si Email:ApiKey no está configurado, no hace nada (la
// app corre igual) — mismo comportamiento que tenía SmtpEmailSender sin Email:Host.
public class BrevoApiEmailSender : IEmailSender
{
    private const string Endpoint = "https://api.brevo.com/v3/smtp/email";
    private readonly IConfiguration _cfg;
    private readonly IHttpClientFactory _httpFactory;

    public BrevoApiEmailSender(IConfiguration cfg, IHttpClientFactory httpFactory)
    {
        _cfg = cfg;
        _httpFactory = httpFactory;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody,
        IEnumerable<EmailAttachment>? attachments = null)
    {
        var apiKey = _cfg["Email:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(toEmail)) return;

        var fromEmail = _cfg["Email:From"] ?? "no-reply@local";
        var fromName = _cfg["Email:FromName"] ?? "Training Platform";

        var payload = new JsonObject
        {
            ["sender"] = new JsonObject { ["name"] = fromName, ["email"] = fromEmail },
            ["to"] = new JsonArray(new JsonObject
            {
                ["email"] = toEmail,
                ["name"] = string.IsNullOrWhiteSpace(toName) ? toEmail : toName
            }),
            ["subject"] = subject,
            ["htmlContent"] = htmlBody
        };

        var lista = attachments?.ToList();
        if (lista is { Count: > 0 })
        {
            var arr = new JsonArray();
            foreach (var a in lista)
                arr.Add(new JsonObject { ["name"] = a.FileName, ["content"] = Convert.ToBase64String(a.Content) });
            payload["attachment"] = arr;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Add("api-key", apiKey);
        req.Headers.Add("Accept", "application/json");
        req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var client = _httpFactory.CreateClient();
        var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Brevo API respondió {(int)res.StatusCode}: {body}");
        }
    }
}

// ---- Resolución del catálogo / pendientes de un usuario ----
// Un curso "sale" mientras el usuario NO lo tenga aprobado y vigente. Estados:
// not-started | in-progress | pending-grading | pending-cancellation | failed |
// renewal (vigente pero reabierto para renovar) | expired (venció) |
// current (aprobado y vigente) | done (aprobado, sin caducidad).
public record PendingItem(Guid TrainingId, string Title, Guid VersionId, Guid? SetId, string Status, DateTime? ExpiresAt);

// Avisos por curso. Lectura tolerante: si el blob falta o está corrupto, se
// comporta como el valor por defecto (avisa al abrir, y 15 y 5 días antes).
public class NotificationConfig
{
    public bool Enabled { get; set; } = true;
    public bool OnOpen { get; set; } = true;
    public List<int> DaysBefore { get; set; } = new() { 15, 5 };

    public static NotificationConfig Parse(string? json)
    {
        var cfg = new NotificationConfig();
        try
        {
            var n = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!)?.AsObject();
            if (n is null) return cfg;
            if (n["enabled"] is not null) cfg.Enabled = n["enabled"]!.GetValue<bool>();
            if (n["onOpen"] is not null) cfg.OnOpen = n["onOpen"]!.GetValue<bool>();
            if (n["daysBefore"] is System.Text.Json.Nodes.JsonArray arr)
                cfg.DaysBefore = arr.Where(x => x is not null).Select(x => x!.GetValue<int>())
                                    .Where(d => d is > 0 and <= 365).Distinct().OrderByDescending(d => d).ToList();
        }
        catch { return new NotificationConfig(); }
        return cfg;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        enabled = Enabled,
        onOpen = OnOpen,
        daysBefore = DaysBefore
    });
}

public static class CatalogLogic
{
    public static async Task<List<PendingItem>> ResolveAsync(TenantDbContext db, Guid? userId, List<Guid> cohortIds)
    {
        var published = await (from v in db.TrainingVersions
                               where v.Status == "published"
                               join t in db.Trainings on v.TrainingId equals t.Id
                               where t.Status != "archived"   // archivado = fuera del catálogo, sin borrar nada
                               select new { trainingId = t.Id, t.Title, t.RecurrenceMonths, t.ExpiresOn, t.RenewLeadDays, versionId = v.Id, v.VersionNumber })
                              .ToListAsync();

        var latest = published.GroupBy(x => x.trainingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.VersionNumber).First());

        var myAttempts = await (from a in db.Attempts
                                where a.UserId == userId
                                join v in db.TrainingVersions on a.TrainingVersionId equals v.Id
                                select new { v.TrainingId, a.Status, a.Passed, a.CompletedAt }).ToListAsync();

        var sets = await db.TrainingSets.ToListAsync();
        var assigns = await db.Assignments.ToListAsync();
        var now = DateTime.UtcNow;

        var result = new List<PendingItem>();
        foreach (var kv in latest)
        {
            var tId = kv.Key; var v = kv.Value;
            var att = myAttempts.Where(x => x.TrainingId == tId).ToList();

            string status; DateTime? expiresAt = null;

            if (att.Any(x => x.Status == "in-progress")) status = "in-progress";
            else if (att.Any(x => x.Status == "cancellation-requested")) status = "pending-cancellation";
            else if (att.Any(x => x.Status == "pending-grading")) status = "pending-grading";
            else
            {
                var lastPass = att.Where(x => x.Passed && x.CompletedAt != null)
                                  .OrderByDescending(x => x.CompletedAt).FirstOrDefault();
                if (lastPass != null)
                {
                    var expiry = VigenciaDe(v.ExpiresOn, v.RecurrenceMonths, lastPass.CompletedAt!.Value, now);
                    if (expiry is null) status = "done";   // una sola vez: aprobado para siempre
                    else
                    {
                        var renewalOpen = expiry.Value.AddDays(-Math.Max(0, v.RenewLeadDays));
                        expiresAt = expiry;
                        if (now < renewalOpen) status = "current";                 // vigente, aún no toca renovar
                        else status = now >= expiry.Value ? "expired" : "renewal"; // reabierto / vencido
                    }
                }
                else if (att.Any(x => x.Status == "completed")) status = "failed";
                else status = "not-started";
            }

            Guid? setId = null;
            var tSets = sets.Where(s => s.TrainingId == tId).ToList();
            if (tSets.Count > 0)
            {
                var ua = assigns.Where(a => a.TrainingId == tId && a.TargetType == "user" && a.TargetId == userId)
                                .OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                if (ua != null) setId = ua.SetId;
                else
                {
                    var ga = assigns.Where(a => a.TrainingId == tId && a.TargetType == "group" && cohortIds.Contains(a.TargetId))
                                    .OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                    setId = ga != null ? ga.SetId : (tSets.FirstOrDefault(x => x.IsDefault)?.Id ?? tSets.First().Id);
                }
            }
            result.Add(new PendingItem(tId, v.Title, v.versionId, setId, status, expiresAt));
        }
        return result;
    }

    // Cuándo caduca la aprobación de una persona.
    //   sin fecha fija ni meses -> nunca
    //   solo meses              -> aprobación + N meses (relativo a cada quien)
    //   solo fecha fija         -> esa fecha, igual para todos
    //   fecha fija + meses      -> la fecha fija rodando de ciclo en ciclo (31/dic cada 12 meses),
    //                              y si alguien aprueba dentro del último ciclo antes del corte,
    //                              se le cuenta el ciclo siguiente para no exigirle repetirlo en días.
    public static DateTime? VigenciaDe(DateTime? fechaFija, int? meses, DateTime aprobadoEn, DateTime ahora)
    {
        if (fechaFija is null && meses is null) return null;
        if (fechaFija is null) return aprobadoEn.AddMonths(meses!.Value);

        var corte = fechaFija.Value;
        if (meses is null) return corte;                 // fecha fija que no se repite

        var paso = Math.Max(1, meses.Value);
        while (corte <= aprobadoEn) corte = corte.AddMonths(paso);   // el corte vigente para esa aprobación
        while (corte <= ahora) corte = corte.AddMonths(paso);        // y no devolver un corte ya pasado
        return corte;
    }
}

// ---- Plantillas HTML de correo ----
public static class EmailTemplates
{
    // Maquetado con tablas y estilos en línea: es lo único que respetan Outlook,
    // Gmail y el correo de iPhone por igual. Ancho fijo de 600px, tipografía del
    // sistema con respaldo Arial, y todo el color puesto a mano (los clientes de
    // correo ignoran <style> y las hojas externas).
    // Mismo logo del ícono de la app (wwwroot/index.html, la "brand" del login),
    // rasterizado a PNG e incrustado como data URL: los clientes de correo no
    // respetan <svg> por igual, pero sí un <img> con data URL.
    private const string LogoDataUri =
        "data:image/png;base64,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";

    private const string Wrap =
        "<!doctype html><html><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        "<meta name=\"color-scheme\" content=\"light\"></head>" +
        "<body style=\"margin:0;padding:0;background:#eef2f7;\">" +
        "<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">{PREHEADER}</div>" +
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#eef2f7;padding:28px 12px;\">" +
        "<tr><td align=\"center\">" +
        "<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:600px;max-width:100%;background:#ffffff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;\">" +

        // Cabecera
        "<tr><td style=\"background:#0b1220;padding:20px 26px;\">" +
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>" +
        "<td style=\"padding-right:10px;\">" +
        "<img src=\"" + LogoDataUri + "\" width=\"30\" height=\"30\" alt=\"Aprendor\" style=\"display:block;border-radius:8px;\">" +
        "</td>" +
        "<td style=\"font:700 18px/1.2 Segoe UI,Arial,sans-serif;color:#ffffff;letter-spacing:.2px;\">Aprendor" +
        "<div style=\"font:400 12px/1.4 Segoe UI,Arial,sans-serif;color:#94a3b8;margin-top:2px;\">Aprende. Cumple. Avanza.</div>" +
        "</td></tr></table></td></tr>" +

        // Cuerpo
        "<tr><td style=\"padding:26px;font:400 15px/1.6 Segoe UI,Arial,sans-serif;color:#0f172a;\">{BODY}</td></tr>" +

        // Pie
        "<tr><td style=\"padding:16px 26px;background:#f8fafc;border-top:1px solid #e2e8f0;" +
        "font:400 12px/1.5 Segoe UI,Arial,sans-serif;color:#64748b;\">" +
        "Mensaje automático de Aprendor. No respondas a este correo." +
        "</td></tr></table>" +
        "<div style=\"font:400 11px/1.5 Segoe UI,Arial,sans-serif;color:#94a3b8;padding-top:14px;\">Plataforma de adiestramientos y cumplimiento</div>" +
        "</td></tr></table></body></html>";

    private static string Render(string body, string preheader)
        => Wrap.Replace("{BODY}", body).Replace("{PREHEADER}", System.Net.WebUtility.HtmlEncode(preheader ?? ""));

    // Botón "a prueba de balas": tabla en vez de <a> con padding, que Outlook rompe.
    private static string Boton(string texto, string url)
        => string.IsNullOrWhiteSpace(url) ? "" :
           "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:24px 0 8px;\"><tr>" +
           $"<td style=\"background:#4f46e5;border-radius:10px;\">" +
           $"<a href=\"{System.Net.WebUtility.HtmlEncode(url)}\" style=\"display:inline-block;padding:13px 26px;" +
           "font:700 15px/1 Segoe UI,Arial,sans-serif;color:#ffffff;text-decoration:none;border-radius:10px;\">" +
           $"{System.Net.WebUtility.HtmlEncode(texto)}</a></td></tr></table>";

    // Recuadro de datos (fechas, credenciales, códigos).
    private static string Recuadro(string contenidoHtml)
        => "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" " +
           "style=\"background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;margin:18px 0;\">" +
           $"<tr><td style=\"padding:14px 16px;font:400 14px/1.6 Segoe UI,Arial,sans-serif;color:#334155;\">{contenidoHtml}</td></tr></table>";

    private static string Titulo(string texto)
        => $"<div style=\"font:700 20px/1.35 Segoe UI,Arial,sans-serif;color:#0f172a;margin:0 0 12px;\">{System.Net.WebUtility.HtmlEncode(texto)}</div>";

    private static string Li(string title) => $"<li style=\"margin:4px 0\">{System.Net.WebUtility.HtmlEncode(title)}</li>";

    public static string Digest(string name, List<PendingItem> notStarted, List<PendingItem> inProgress)
    {
        var b = new System.Text.StringBuilder();
        b.Append(Titulo("Tus adiestramientos pendientes"));
        b.Append($"<p style=\"margin:0 0 10px;\">Hola {System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? "" : name)},</p>");
        if (notStarted.Count > 0)
        {
            b.Append("<p style=\"font-weight:600;margin-bottom:4px\">Sin comenzar</p><ul style=\"margin-top:0\">");
            foreach (var i in notStarted) b.Append(Li(i.Title));
            b.Append("</ul>");
        }
        if (inProgress.Count > 0)
        {
            b.Append("<p style=\"font-weight:600;margin-bottom:4px\">En progreso / por completar</p><ul style=\"margin-top:0\">");
            foreach (var i in inProgress) b.Append(Li(i.Title));
            b.Append("</ul>");
        }
        b.Append("<p style=\"margin:16px 0 0;color:#475569;\">Entra a la plataforma para completarlos.</p>");
        return Render(b.ToString(), $"Tienes {notStarted.Count + inProgress.Count} adiestramiento(s) pendiente(s).");
    }

    public static string Invitation(string name, string email, string tempPassword, string? appUrl)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Bienvenido a Aprendor") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Tu cuenta ya está lista para que comiences tus adiestramientos.</p>" +
            Recuadro(
                $"<div style=\"margin:2px 0;\"><span style=\"color:#64748b;\">Usuario:</span> <b>{Enc(email)}</b></div>" +
                $"<div style=\"margin:6px 0 2px;\"><span style=\"color:#64748b;\">Contraseña temporal:</span> " +
                $"<b style=\"font-family:Consolas,monospace;\">{Enc(tempPassword)}</b></div>") +
            "<p style=\"margin:0;color:#475569;font-size:14px;\">Por seguridad, la primera vez que inicies sesión te pediremos crear tu propia contraseña.</p>" +
            Boton("Entrar a Aprendor", appUrl ?? "");
        return Render(body, "Tu cuenta de Aprendor ya está lista.");
    }

    public static string PasswordReset(string name, string link, int minutos)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Restablecer tu contraseña") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Recibimos una solicitud para restablecer la contraseña de tu cuenta. " +
            "Pulsa el botón para crear una nueva.</p>" +
            Boton("Restablecer contraseña", link) +
            $"<p style=\"margin:14px 0 0;color:#64748b;font-size:13px;\">El enlace vence en {minutos} minutos y solo se puede usar una vez.</p>" +
            "<p style=\"margin:6px 0 0;color:#64748b;font-size:13px;\">Si no fuiste tú, ignora este mensaje: tu contraseña actual sigue funcionando.</p>";
        return Render(body, "Enlace para crear una contraseña nueva.");
    }

    // Aviso de curso disponible y recordatorios de vencimiento.
    public static string CourseReminder(string name, string title, string tipo, DateTime? expiresAt, string? appUrl)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var dias = tipo.StartsWith("due") ? tipo[3..] : null;
        var (encabezado, cuerpo) = dias is null
            ? ("Tienes un adiestramiento disponible",
               $"El adiestramiento <b>{Enc(title)}</b> ya está disponible para que lo tomes.")
            : ($"Te quedan {dias} días",
               $"El adiestramiento <b>{Enc(title)}</b> vence en <b>{dias} días</b> y todavía no lo has completado.");
        var vence = expiresAt is DateTime d
            ? Recuadro($"<span style=\"color:#64748b;\">Fecha de vencimiento:</span> <b>{d:dd/MM/yyyy}</b>")
            : "";
        var body =
            Titulo(encabezado) +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            $"<p style=\"margin:0;\">{cuerpo}</p>" +
            vence +
            Boton("Tomar el adiestramiento", appUrl ?? "");
        return Render(body, dias is null ? $"{title} ya está disponible." : $"{title} vence en {dias} días.");
    }

    // Certificado emitido. Dos versiones: la que recibe el propio empleado y la
    // que recibe quien lo archiva en su expediente.
    public static string CertificateIssued(string learnerName, string trainingTitle, string serial,
        DateTime issuedAt, DateTime? expiresAt, bool paraArchivo, string? appUrl)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var datos =
            $"<div style=\"font:700 16px/1.4 Segoe UI,Arial,sans-serif;color:#0f172a;\">{Enc(trainingTitle)}</div>" +
            (paraArchivo ? $"<div style=\"margin-top:8px;\"><span style=\"color:#64748b;\">Empleado:</span> <b>{Enc(learnerName)}</b></div>" : "") +
            $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Emitido:</span> <b>{issuedAt.ToLocalTime():dd/MM/yyyy}</b></div>" +
            (expiresAt is DateTime v ? $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Vigente hasta:</span> <b>{v.ToLocalTime():dd/MM/yyyy}</b></div>" : "") +
            $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Folio:</span> <b style=\"font-family:Consolas,monospace;\">{Enc(serial)}</b></div>";

        var body = paraArchivo
            ? Titulo("Certificado para expediente") +
              $"<p style=\"margin:0;\"><b>{Enc(learnerName)}</b> aprobó un adiestramiento. El certificado va adjunto en PDF para que lo archives en su expediente.</p>" +
              Recuadro(datos)
            : Titulo("¡Felicidades! Aquí está tu certificado") +
              $"<p style=\"margin:0 0 4px;\">Hola {Enc(learnerName)},</p>" +
              "<p style=\"margin:0;\">Completaste tu adiestramiento. Te adjuntamos el certificado en PDF; también queda guardado en tu expediente dentro de la plataforma.</p>" +
              Recuadro(datos);

        return Render(body + Boton(paraArchivo ? "Ver en la plataforma" : "Ver mi expediente", appUrl ?? ""),
            paraArchivo ? $"Certificado de {learnerName}: {trainingTitle}" : $"Tu certificado de {trainingTitle}");
    }

    public static string VerifyEmail(string name, string code, int minutos)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Valida tu correo") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Escribe este código en la plataforma para confirmar que este correo es tuyo. " +
            "A esta dirección te enviaremos los certificados de los adiestramientos que apruebes.</p>" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" " +
            "style=\"background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;margin:18px 0;\"><tr>" +
            "<td align=\"center\" style=\"padding:18px;font:700 32px/1 Consolas,Menlo,monospace;letter-spacing:8px;color:#0f172a;\">" +
            $"{Enc(code)}</td></tr></table>" +
            $"<p style=\"margin:0;color:#64748b;font-size:13px;\">Vence en {minutos} minutos.</p>";
        return Render(body, "Código para validar tu correo en Aprendor.");
    }

    public static string TwoFactorCode(string name, string code, int minutos)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Tu código de verificación") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Usa este código para completar tu inicio de sesión:</p>" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" " +
            "style=\"background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;margin:18px 0;\"><tr>" +
            "<td align=\"center\" style=\"padding:18px;font:700 32px/1 Consolas,Menlo,monospace;letter-spacing:8px;color:#0f172a;\">" +
            $"{Enc(code)}</td></tr></table>" +
            $"<p style=\"margin:0;color:#64748b;font-size:13px;\">Vence en {minutos} minutos y solo se puede usar una vez.</p>" +
            "<p style=\"margin:6px 0 0;color:#64748b;font-size:13px;\">Si no fuiste tú quien intentó entrar, cambia tu contraseña.</p>";
        return Render(body, "Código de verificación de Aprendor.");
    }

    public static string Completion(string learnerName, string trainingTitle, int score, int total)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var pct = total > 0 ? (int)Math.Round(score * 100.0 / total) : 100;
        var body =
            Titulo("Adiestramiento completado") +
            $"<p style=\"margin:0;\"><b>{Enc(learnerName ?? "(sin nombre)")}</b> completó satisfactoriamente:</p>" +
            Recuadro(
                $"<div style=\"font:700 16px/1.4 Segoe UI,Arial,sans-serif;color:#0f172a;\">{Enc(trainingTitle ?? "")}</div>" +
                $"<div style=\"margin-top:8px;\"><span style=\"color:#64748b;\">Puntuación:</span> <b>{score}/{total}</b> ({pct}%)</div>");
        return Render(body, $"{Enc(learnerName ?? "")} completó {Enc(trainingTitle ?? "")}.");
    }
}

// ---- Alerta a autores/moderadores cuando alguien aprueba un curso ----
public static class CompletionAlert
{
    public static async Task SendAsync(CatalogDbContext catalog, IEmailSender email,
        Guid? tenantId, string? learnerName, string? trainingTitle, int score, int total)
    {
        if (tenantId is null) return;
        var recipients = (await CompanyUsers.OfAsync(catalog, tenantId.Value))
            .Where(u => u.Role is "Author" or "Moderator" or "Admin")
            .Select(u => new { u.Email, u.Name }).ToList();
        var html = EmailTemplates.Completion(learnerName ?? "", trainingTitle ?? "", score, total);
        foreach (var r in recipients)
        {
            try { await email.SendAsync(r.Email, r.Name, $"Curso completado: {trainingTitle}", html); }
            catch { /* no romper el flujo por un fallo de correo */ }
        }
    }
}

// ---- Construcción y envío del resumen semanal (todas las bases de clientes) ----
public static class DigestRunner
{
    public static async Task<int> RunAsync(IServiceProvider sp, IEmailSender email)
    {
        using var scope = sp.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var tenants = await catalog.Tenants.Where(t => t.Status == "active").ToListAsync();

        int sent = 0;
        foreach (var t in tenants)
        {
            var opts = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(t.ConnectionString).Options;
            await using var db = new TenantDbContext(opts);

            var users = await CompanyUsers.OfAsync(catalog, t.Id);

            foreach (var u in users)
            {
                var cohortIds = await db.UserGroupMembers.Where(m => m.UserId == u.Id)
                    .Select(m => m.UserGroupId).ToListAsync();
                var pending = await CatalogLogic.ResolveAsync(db, u.Id, cohortIds);
                var notStarted = pending.Where(p => p.Status == "not-started").ToList();
                var inProgress = pending.Where(p => p.Status == "in-progress" || p.Status == "failed"
                                                 || p.Status == "renewal" || p.Status == "expired").ToList();
                if (notStarted.Count == 0 && inProgress.Count == 0) continue;

                try
                {
                    await email.SendAsync(u.Email, u.Name, "Adiestramientos pendientes",
                        EmailTemplates.Digest(u.Name, notStarted, inProgress));
                    sent++;
                }
                catch { /* continuar con el resto */ }
            }
        }
        return sent;
    }
}

// Corre el resumen semanal en el día/hora configurados (Email:Digest*). Opt-in.
// ---- Recordatorios al learner: curso disponible, y 15 y 5 días antes de vencer ----
// La idempotencia la da NotificationLog, no el reloj: se puede correr varias veces
// al día sin repetir avisos, y si el servidor estuvo caído el aviso sale al volver.
public static class ReminderRunner
{
    public static async Task<int> RunAsync(IServiceProvider sp, IEmailSender email, IConfiguration cfg)
    {
        using var scope = sp.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var appUrl = cfg["App:BaseUrl"];
        var tenants = await catalog.Tenants.Where(t => t.Status == "active").ToListAsync();

        int enviados = 0;
        foreach (var tenant in tenants)
        {
            var usuarios = await CompanyUsers.OfAsync(catalog, tenant.Id);
            if (usuarios.Count == 0) continue;

            var opts = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(tenant.ConnectionString).Options;
            await using var db = new TenantDbContext(opts);

            var hoy = DateTime.UtcNow;
            foreach (var u in usuarios)
            {
                List<PendingItem> pendientes;
                try
                {
                    var cohortes = await db.UserGroupMembers.Where(m => m.UserId == u.Id)
                        .Select(m => m.UserGroupId).ToListAsync();
                    pendientes = await CatalogLogic.ResolveAsync(db, u.Id, cohortes);
                }
                catch { continue; }

                foreach (var p in pendientes)
                {
                    // Solo lo que el learner puede tomar ahora y no ha completado.
                    var tomable = p.Status is "not-started" or "renewal" or "expired" or "failed";
                    if (!tomable) continue;

                    // Cada curso decide si avisa y con cuánta anticipación.
                    var cfgCurso = await db.Trainings.Where(x => x.Id == p.TrainingId)
                        .Select(x => x.NotificationConfigJson).FirstOrDefaultAsync();
                    var reglas = NotificationConfig.Parse(cfgCurso);
                    if (!reglas.Enabled) continue;

                    var avisos = new List<(string kind, string tipo)>();

                    // Disponible: una vez por versión publicada (una versión nueva vuelve a avisar).
                    if (reglas.OnOpen) avisos.Add(($"open:{p.VersionId}", "open"));

                    // Vencimiento: un aviso por cada anticipación configurada, la más cercana que aplique.
                    if (p.ExpiresAt is DateTime vence && reglas.DaysBefore.Count > 0)
                    {
                        var dias = (vence.Date - hoy.Date).TotalDays;
                        var sello = vence.ToString("yyyyMMdd");
                        foreach (var d in reglas.DaysBefore.OrderByDescending(x => x))
                            if (dias <= d && dias >= 0)
                            {
                                avisos.Add(($"due{d}:{sello}", $"due{d}"));
                                break;   // solo el escalón más cercano
                            }
                    }

                    foreach (var (kind, tipo) in avisos)
                    {
                        var yaEnviado = await db.NotificationLogs
                            .AnyAsync(n => n.UserId == u.Id && n.TrainingId == p.TrainingId && n.Kind == kind);
                        if (yaEnviado) continue;

                        db.NotificationLogs.Add(new NotificationLog
                        {
                            UserId = u.Id, TrainingId = p.TrainingId, Kind = kind, SentAt = DateTime.UtcNow
                        });
                        try { await db.SaveChangesAsync(); }
                        catch (DbUpdateException) { continue; }   // otro proceso lo mandó primero

                        try
                        {
                            var asunto = tipo == "open"
                                ? $"Adiestramiento disponible: {p.Title}"
                                : $"Te quedan {tipo[3..]} días: {p.Title}";
                            await email.SendAsync(u.Email, u.Name, asunto,
                                EmailTemplates.CourseReminder(u.Name, p.Title, tipo, p.ExpiresAt, appUrl));
                            enviados++;
                        }
                        catch { /* el registro ya quedó; no se reintenta para no spamear */ }
                    }
                }
            }
        }
        return enviados;
    }
}

public class ReminderService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;

    public ReminderService(IServiceProvider sp, IEmailSender email, IConfiguration cfg)
    { _sp = sp; _email = email; _cfg = cfg; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Encendido por defecto: sin SMTP configurado el envío no hace nada igual.
        if (bool.TryParse(_cfg["Email:RemindersEnabled"], out var en) && !en) return;

        await Task.Delay(TimeSpan.FromMinutes(2), ct);   // deja arrancar la app
        while (!ct.IsCancellationRequested)
        {
            try { await ReminderRunner.RunAsync(_sp, _email, _cfg); }
            catch (OperationCanceledException) { break; }
            catch { /* reintenta en el próximo ciclo */ }

            try { await Task.Delay(TimeSpan.FromHours(6), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}

public class WeeklyDigestService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;

    public WeeklyDigestService(IServiceProvider sp, IEmailSender email, IConfiguration cfg, IHostEnvironment env)
    { _sp = sp; _email = email; _cfg = cfg; _env = env; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!(bool.TryParse(_cfg["Email:DigestEnabled"], out var en) && en)) return; // apagado por defecto

        var freq = (_cfg["Email:DigestFrequency"] ?? "Weekly").Trim().ToLowerInvariant(); // daily | weekly | monthly
        var hour = int.TryParse(_cfg["Email:DigestHour"], out var h) ? h : 8;             // hora mínima de envío
        var dayOfWeek = Enum.TryParse<DayOfWeek>(_cfg["Email:DigestDayOfWeek"], true, out var dw) ? dw : DayOfWeek.Monday;
        var dayOfMonth = int.TryParse(_cfg["Email:DigestDayOfMonth"], out var dm) ? dm : 1;

        var marker = Path.Combine(_env.ContentRootPath, "App_Data", "last_digest.txt");
        try { Directory.CreateDirectory(Path.GetDirectoryName(marker)!); } catch { }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                if (TryDigestKey(now, freq, hour, dayOfWeek, dayOfMonth, out var key))
                {
                    var last = File.Exists(marker) ? (await File.ReadAllTextAsync(marker, ct)).Trim() : "";
                    if (last != key)
                    {
                        await DigestRunner.RunAsync(_sp, _email);
                        await File.WriteAllTextAsync(marker, key, ct);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* reintentar en el próximo ciclo */ }

            try { await Task.Delay(TimeSpan.FromMinutes(30), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Si AHORA cae en la ventana de envío, devuelve true y la llave del periodo.
    // La llave identifica cada periodo, así solo se envía una vez por periodo.
    private static bool TryDigestKey(DateTime now, string freq, int hour,
        DayOfWeek dayOfWeek, int dayOfMonth, out string key)
    {
        key = "";
        if (now.Hour < hour) return false;
        switch (freq)
        {
            case "daily":
                key = now.ToString("yyyy-MM-dd");
                return true;
            case "monthly":
                // Acota el día al último del mes (p. ej. 31 en un mes de 30).
                var target = Math.Min(dayOfMonth < 1 ? 1 : dayOfMonth, DateTime.DaysInMonth(now.Year, now.Month));
                if (now.Day != target) return false;
                key = now.ToString("yyyy-MM");
                return true;
            case "weekly":
            default:
                if (now.DayOfWeek != dayOfWeek) return false;
                key = $"{now.Year}-W{ISOWeek.GetWeekOfYear(now)}";
                return true;
        }
    }
}
