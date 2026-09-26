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
using TrainingPlatform.Multitenancy;
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
// overdue (sin aprobar y con la fecha límite pasada) |
// renewal (vigente pero reabierto para renovar) | expired (venció) |
// current (aprobado y vigente) | done (aprobado, sin caducidad).
// ExpiresAt = cuándo caduca la aprobación que ya tiene; DueAt = fecha límite para
// completarlo por primera vez (plan del grupo u onboarding de la compañía).
public record PendingItem(Guid TrainingId, string Title, Guid VersionId, Guid? SetId, string Status, DateTime? ExpiresAt,
    DateTime? DueAt = null);

// Avisos por curso. Lectura tolerante: si el blob falta o está corrupto, se
// comporta como el valor por defecto (avisa al abrir, 15 y 5 días antes, y cada
// 14 días mientras siga vencido).
public class NotificationConfig
{
    public bool Enabled { get; set; } = true;
    public bool OnOpen { get; set; } = true;
    public List<int> DaysBefore { get; set; } = new() { 15, 5 };
    // Cada cuántos días se le repite el aviso al empleado mientras el curso siga
    // vencido (estado expired u overdue). 0 = apagado.
    public int OverdueEveryDays { get; set; } = 14;

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
        // Aparte: un valor ilegible aquí no debe tumbar el resto de la configuración.
        try
        {
            var n = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!) as System.Text.Json.Nodes.JsonObject;
            if (n?["overdueEveryDays"] is System.Text.Json.Nodes.JsonNode od)
                cfg.OverdueEveryDays = Math.Clamp(od.GetValue<int>(), 0, 365);
        }
        catch { }
        return cfg;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        enabled = Enabled,
        onOpen = OnOpen,
        daysBefore = DaysBefore,
        overdueEveryDays = Math.Clamp(OverdueEveryDays, 0, 365)
    });
}

public static class CatalogLogic
{
    // Fecha de ingreso de una persona a la compañía: la de su membresía en UserCompany
    // o, si la compañía es su principal (AppUser.TenantId), la de creación de la cuenta.
    // Es el punto de partida del plazo de onboarding de los cursos `everyone`.
    public static async Task<DateTime?> FechaIngresoAsync(CatalogDbContext catalog, Guid? userId, Guid? tenantId)
    {
        if (userId is null || tenantId is null) return null;
        var extra = await catalog.UserCompanies.Where(m => m.UserId == userId && m.TenantId == tenantId)
            .Select(m => (DateTime?)m.CreatedAt).FirstOrDefaultAsync();
        if (extra is not null) return extra;
        return await catalog.Users.Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => (DateTime?)u.CreatedAt).FirstOrDefaultAsync();
    }

    // Lo mismo para todos los miembros de una compañía de una vez (para los procesos
    // que recorren usuario por usuario: recordatorios, resúmenes).
    public static async Task<Dictionary<Guid, DateTime>> FechasIngresoAsync(CatalogDbContext catalog, Guid tenantId)
    {
        var fechas = await catalog.Users.Where(u => u.TenantId == tenantId)
            .Select(u => new { u.Id, Fecha = u.CreatedAt }).ToListAsync();
        var extras = await catalog.UserCompanies.Where(m => m.TenantId == tenantId)
            .Select(m => new { Id = m.UserId, Fecha = m.CreatedAt }).ToListAsync();
        var d = new Dictionary<Guid, DateTime>();
        foreach (var f in fechas.Concat(extras)) d.TryAdd(f.Id, f.Fecha);
        return d;
    }

    // Qué cursos le tocan a una persona y en qué estado está cada uno.
    //
    // Obligatoriedad: un curso publicado sale si `Training.Audience == everyone` o si
    // algún grupo del usuario lo tiene en su plan (GroupCourse). Un curso `groups` que
    // no está en los planes del usuario sólo sale si ya lo tomó (para que conserve su
    // historial y sus aprobados), y nunca con fecha límite.
    //
    // Fecha límite (DueAt), sólo mientras el curso NO esté aprobado y vigente:
    //   - por grupo:  max(JoinedAt del miembro, AddedAt del curso en el plan)
    //                 + (GroupCourse.DueDays ?? UserGroup.OnboardingDays) días;
    //                 si está en varios grupos, la más temprana.
    //   - `everyone` con Training.OnboardingDays: fechaIngreso + OnboardingDays
    //                 (fechaIngreso = UserCompany.CreatedAt; si no se pasa, sin límite).
    //   - si aplican ambas, la más temprana.
    // Quien ya tenía el curso aprobado y vigente al entrar al grupo no lo vuelve a
    // deber: DueAt queda en null y el estado sigue siendo current/done.
    // Estado `overdue`: not-started / in-progress / failed con DueAt ya pasado.
    // ExpiresAt sigue siendo la caducidad de una aprobación (renewal/expired/current).
    //
    // `cohortIds` son los grupos del usuario (se mantienen por compatibilidad con quien
    // ya llama; JoinedAt se lee aquí). `fechaIngreso` es opcional: sin ella los cursos
    // `everyone` no llevan fecha límite.
    public static async Task<List<PendingItem>> ResolveAsync(TenantDbContext db, Guid? userId, List<Guid> cohortIds,
        DateTime? fechaIngreso = null)
    {
        var published = await (from v in db.TrainingVersions
                               where v.Status == "published"
                               join t in db.Trainings on v.TrainingId equals t.Id
                               where t.Status != "archived"   // archivado = fuera del catálogo, sin borrar nada
                               select new { trainingId = t.Id, t.Title, t.RecurrenceMonths, t.ExpiresOn, t.RenewLeadDays,
                                            t.Audience, t.OnboardingDays, versionId = v.Id, v.VersionNumber })
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

        // Plan de los grupos del usuario: por curso, la fecha límite más temprana.
        var limitesPorGrupo = new Dictionary<Guid, DateTime>();
        var enPlan = new HashSet<Guid>();
        if (userId is not null && cohortIds.Count > 0)
        {
            var membresias = await db.UserGroupMembers
                .Where(m => m.UserId == userId && cohortIds.Contains(m.UserGroupId))
                .Select(m => new { m.UserGroupId, m.JoinedAt }).ToListAsync();
            var grupos = await db.UserGroups.Where(g => cohortIds.Contains(g.Id))
                .Select(g => new { g.Id, g.OnboardingDays }).ToDictionaryAsync(g => g.Id, g => g.OnboardingDays);
            var plan = await db.GroupCourses.Where(c => cohortIds.Contains(c.UserGroupId)).ToListAsync();
            foreach (var c in plan)
            {
                enPlan.Add(c.TrainingId);
                var desde = membresias.FirstOrDefault(m => m.UserGroupId == c.UserGroupId)?.JoinedAt ?? now;
                if (c.AddedAt > desde) desde = c.AddedAt;
                var dias = c.DueDays ?? (grupos.TryGetValue(c.UserGroupId, out var d) ? d : 7);
                var limite = desde.AddDays(Math.Max(0, dias));
                if (!limitesPorGrupo.TryGetValue(c.TrainingId, out var actual) || limite < actual)
                    limitesPorGrupo[c.TrainingId] = limite;
            }
        }

        var result = new List<PendingItem>();
        foreach (var kv in latest)
        {
            var tId = kv.Key; var v = kv.Value;
            var att = myAttempts.Where(x => x.TrainingId == tId).ToList();

            // Obligatorio para esta persona: para todos, o por el plan de alguno de sus grupos.
            var obligatorio = v.Audience != "groups" || enPlan.Contains(tId);
            if (!obligatorio && att.Count == 0) continue;   // ni le toca ni lo ha tomado

            string status; DateTime? expiresAt = null; DateTime? dueAt = null;

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

            // Fecha límite sólo mientras no lo tenga aprobado (ver regla arriba).
            if (obligatorio && status is "not-started" or "in-progress" or "failed")
            {
                if (limitesPorGrupo.TryGetValue(tId, out var porGrupo)) dueAt = porGrupo;
                if (v.Audience != "groups" && v.OnboardingDays is int od && fechaIngreso is DateTime ingreso)
                {
                    var porIngreso = ingreso.AddDays(Math.Max(0, od));
                    if (dueAt is null || porIngreso < dueAt) dueAt = porIngreso;
                }
                if (dueAt is DateTime limite && now >= limite) status = "overdue";
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
            result.Add(new PendingItem(tId, v.Title, v.versionId, setId, status, expiresAt, dueAt));
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

    // Invitación sin contraseña en claro: enlace de un solo uso para que la persona cree
    // su propia clave.
    public static string InvitationLink(string name, string email, string link, int horas)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Bienvenido a Aprendor") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Te crearon una cuenta para que tomes tus adiestramientos. " +
            "Pulsa el botón para crear tu contraseña y entrar.</p>" +
            Recuadro($"<div style=\"margin:2px 0;\"><span style=\"color:#64748b;\">Usuario:</span> <b>{Enc(email)}</b></div>") +
            Boton("Crear mi contraseña", link) +
            $"<p style=\"margin:14px 0 0;color:#64748b;font-size:13px;\">El enlace vence en {horas} horas y solo se puede usar una vez. " +
            "Si vence, usa «¿Olvidaste tu contraseña?» en la pantalla de entrada o pide otra invitación.</p>";
        return Render(body, "Crea tu contraseña de Aprendor.");
    }

    // Aviso de bloqueo temporal. motivo: password (contraseña) | 2fa (código de verificación).
    public static string AccountLocked(string name, string motivo, int minutos)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var que = motivo == "2fa" ? "códigos de verificación incorrectos" : "contraseñas incorrectas";
        var body =
            Titulo("Bloqueamos temporalmente el acceso") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            $"<p style=\"margin:0;\">Hubo varios intentos seguidos de entrar a tu cuenta con {que}. " +
            $"Por seguridad, el acceso quedó bloqueado durante {minutos} minutos.</p>" +
            "<p style=\"margin:14px 0 0;color:#475569;font-size:14px;\">Si fuiste tú, espera y vuelve a intentarlo. " +
            (motivo == "2fa"
                ? "Si no fuiste tú, alguien conoce tu contraseña: cámbiala en cuanto puedas entrar."
                : "Si no fuiste tú, te recomendamos cambiar tu contraseña con «¿Olvidaste tu contraseña?».") +
            "</p>";
        return Render(body, "Varios intentos fallidos de entrar a tu cuenta.");
    }

    // Aviso de cambios en la verificación en dos pasos. cambio: enabled | changed | disabled.
    public static string AuthenticatorChanged(string name, string cambio, DateTime cuandoUtc)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var (titulo, texto) = cambio switch
        {
            "changed" => ("Cambiaste tu app autenticadora", "Se registró una app autenticadora nueva en tu cuenta. La anterior ya no sirve para entrar."),
            "disabled" => ("Desactivaste la verificación en dos pasos", "Tu cuenta ya no pide el código de la app autenticadora al entrar."),
            _ => ("Activaste la verificación en dos pasos", "A partir de ahora, al entrar te pediremos el código de tu app autenticadora."),
        };
        var body =
            Titulo(titulo) +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            $"<p style=\"margin:0;\">{Enc(texto)}</p>" +
            Recuadro($"<div><span style=\"color:#64748b;\">Fecha:</span> <b>{cuandoUtc:yyyy-MM-dd HH:mm} UTC</b></div>") +
            "<p style=\"margin:0;color:#64748b;font-size:13px;\">Si no fuiste tú, cambia tu contraseña enseguida y avisa al administrador de tu compañía.</p>";
        return Render(body, titulo + ".");
    }

    // Código para «Perdí mi autenticador»: recuperar el acceso quitando la app autenticadora.
    public static string RecoveryCode(string name, string code, int minutos)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Código para recuperar tu acceso") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Alguien que conoce tu contraseña dijo haber perdido tu app autenticadora y pidió " +
            "recuperar el acceso a tu cuenta. Si fuiste tú, escribe este código en la pantalla de entrada:</p>" +
            CodigoGrande(code) +
            $"<p style=\"margin:0;color:#64748b;font-size:13px;\">Vence en {minutos} minutos y solo se puede usar una vez. " +
            "Al usarlo quitaremos la app autenticadora de tu cuenta y tendrás que registrarla de nuevo.</p>" +
            "<p style=\"margin:10px 0 0;color:#b91c1c;font-size:13px;\"><b>Si no fuiste tú, no compartas este código con nadie</b> " +
            "y cambia tu contraseña enseguida: alguien la conoce.</p>";
        return Render(body, "Código para recuperar el acceso a tu cuenta de Aprendor.");
    }

    // Código para registrar la app autenticadora por primera vez desde fuera de una red de
    // confianza: confirma que quien la registra también tiene acceso a este correo.
    public static string EnrollCode(string name, string code, int minutos)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var body =
            Titulo("Confirma el registro de tu app autenticadora") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            "<p style=\"margin:0;\">Para registrar una app autenticadora en tu cuenta, escribe este código en la plataforma:</p>" +
            CodigoGrande(code) +
            $"<p style=\"margin:0;color:#64748b;font-size:13px;\">Vence en {minutos} minutos y solo se puede usar una vez.</p>" +
            "<p style=\"margin:6px 0 0;color:#64748b;font-size:13px;\">Si no fuiste tú, no compartas este código y cambia tu contraseña: alguien la conoce.</p>";
        return Render(body, "Código para registrar tu app autenticadora en Aprendor.");
    }

    // Aviso de que se quitó la app autenticadora de la cuenta.
    // motivo: admin (lo reinició un administrador) | recovered (el propio usuario, con el código por correo).
    public static string AuthenticatorReset(string name, string motivo, DateTime cuandoUtc)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var texto = motivo == "admin"
            ? "Un administrador reinició la verificación en dos pasos de tu cuenta: tu app autenticadora anterior ya no sirve para entrar."
            : "Recuperaste el acceso a tu cuenta con un código enviado a este correo y quitamos tu app autenticadora anterior.";
        var body =
            Titulo("Quitamos tu app autenticadora") +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
            $"<p style=\"margin:0;\">{Enc(texto)}</p>" +
            Recuadro($"<div><span style=\"color:#64748b;\">Fecha:</span> <b>{cuandoUtc:yyyy-MM-dd HH:mm} UTC</b></div>") +
            "<p style=\"margin:0;\">Si tu compañía exige la verificación en dos pasos, al entrar te pediremos registrar una app autenticadora nueva.</p>" +
            "<p style=\"margin:10px 0 0;color:#64748b;font-size:13px;\">Si no fuiste tú ni lo pediste, cambia tu contraseña enseguida y avisa al administrador de tu compañía.</p>";
        return Render(body, "Se quitó la app autenticadora de tu cuenta.");
    }

    // Aviso a los Admin de la compañía de un cambio sensible en la cuenta de alguien.
    // evento: password-reset (restableció su contraseña con el enlace) | 2fa-recovered
    // (recuperó el acceso por correo y se quitó su app autenticadora).
    public static string AdminSecurityNotice(string adminName, string userName, string userEmail, string companyName,
        string evento, DateTime cuandoUtc, string? ip)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var (titulo, texto) = evento == "2fa-recovered"
            ? ("Un usuario recuperó su acceso sin su app autenticadora",
               "recuperó el acceso a su cuenta con un código enviado a su correo y se quitó su app autenticadora.")
            : ("Un usuario restableció su contraseña",
               "restableció su contraseña con un enlace enviado a su correo, desde fuera de las redes de confianza de la compañía.");
        var body =
            Titulo(titulo) +
            $"<p style=\"margin:0 0 10px;\">Hola {Enc(adminName)},</p>" +
            $"<p style=\"margin:0;\"><b>{Enc(string.IsNullOrWhiteSpace(userName) ? userEmail : userName)}</b> {Enc(texto)}</p>" +
            Recuadro(
                $"<div><span style=\"color:#64748b;\">Usuario:</span> <b>{Enc(userEmail)}</b></div>" +
                $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Compañía:</span> <b>{Enc(companyName)}</b></div>" +
                $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Fecha:</span> <b>{cuandoUtc:yyyy-MM-dd HH:mm} UTC</b></div>" +
                (string.IsNullOrWhiteSpace(ip) ? "" : $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">IP:</span> <b>{Enc(ip!)}</b></div>")) +
            "<p style=\"margin:0;color:#64748b;font-size:13px;\">Si no esperabas este cambio, confirma con la persona que fue ella. " +
            "Desde Usuarios puedes reiniciar su doble factor o restablecer su contraseña.</p>";
        return Render(body, titulo + ".");
    }

    private static string CodigoGrande(string code)
        => "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" " +
           "style=\"background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;margin:18px 0;\"><tr>" +
           "<td align=\"center\" style=\"padding:18px;font:700 32px/1 Consolas,Menlo,monospace;letter-spacing:8px;color:#0f172a;\">" +
           $"{System.Net.WebUtility.HtmlEncode(code ?? "")}</td></tr></table>";

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

    // Aviso de curso disponible y recordatorios de vencimiento. Con `esLimite` la fecha
    // es la fecha límite para completarlo por primera vez (plan del grupo / onboarding),
    // no la caducidad de una aprobación anterior.
    // tipo: open | due{N} | overdue (ya vencido: se repite cada OverdueEveryDays días).
    public static string CourseReminder(string name, string title, string tipo, DateTime? expiresAt, string? appUrl,
        bool esLimite = false)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        if (tipo == "overdue")
        {
            var cuando = expiresAt is DateTime f ? $" el <b>{f:dd/MM/yyyy}</b>" : "";
            var cuerpoVencido = esLimite
                ? $"El plazo para completar el adiestramiento <b>{Enc(title)}</b> terminó{cuando} y todavía no lo has aprobado."
                : $"Tu aprobación del adiestramiento <b>{Enc(title)}</b> venció{cuando}. Necesitas tomarlo de nuevo para estar al día.";
            var bodyVencido =
                Titulo("Tienes un adiestramiento vencido") +
                $"<p style=\"margin:0 0 10px;\">Hola {Enc(name)},</p>" +
                $"<p style=\"margin:0;\">{cuerpoVencido}</p>" +
                (expiresAt is DateTime fv
                    ? Recuadro($"<span style=\"color:#64748b;\">{(esLimite ? "Fecha límite:" : "Venció el:")}</span> " +
                               $"<b style=\"color:#b91c1c;\">{fv:dd/MM/yyyy}</b>")
                    : "") +
                "<p style=\"margin:0;color:#475569;\">Complétalo lo antes posible: tu oficial de cumplimiento recibe el listado de adiestramientos vencidos.</p>" +
                Boton("Tomar el adiestramiento", appUrl ?? "");
            return Render(bodyVencido, $"{title} está vencido.");
        }
        var dias = tipo.StartsWith("due") ? tipo[3..] : null;
        var (encabezado, cuerpo) = dias is null
            ? ("Tienes un adiestramiento disponible",
               $"El adiestramiento <b>{Enc(title)}</b> ya está disponible para que lo tomes.")
            : esLimite
            ? ($"Te quedan {dias} días",
               $"Tu plazo para completar el adiestramiento <b>{Enc(title)}</b> termina en <b>{dias} días</b>.")
            : ($"Te quedan {dias} días",
               $"El adiestramiento <b>{Enc(title)}</b> vence en <b>{dias} días</b> y todavía no lo has completado.");
        var vence = expiresAt is DateTime d
            ? Recuadro($"<span style=\"color:#64748b;\">{(esLimite ? "Fecha límite:" : "Fecha de vencimiento:")}</span> <b>{d:dd/MM/yyyy}</b>")
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
    // que recibe quien lo archiva en su expediente (oficial de cumplimiento u otra copia).
    // Con `link` el certificado NO va adjunto: el botón abre el PDF por un enlace que
    // vale `dias` días. Sin `link` es el correo de siempre, con el PDF adjunto.
    public static string CertificateIssued(string learnerName, string trainingTitle, string serial,
        DateTime issuedAt, DateTime? expiresAt, bool paraArchivo, string? appUrl,
        string? link = null, int dias = 0)
    {
        if (!string.IsNullOrWhiteSpace(link))
            return CertificateIssuedLink(learnerName, trainingTitle, serial, issuedAt, expiresAt, paraArchivo, appUrl, link!, dias);

        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var datos = DatosCertificado(learnerName, trainingTitle, serial, issuedAt, expiresAt, paraArchivo);

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

    // Recuadro con los datos del certificado (común a las variantes con adjunto y con enlace).
    private static string DatosCertificado(string learnerName, string trainingTitle, string serial,
        DateTime issuedAt, DateTime? expiresAt, bool paraArchivo)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        return
            $"<div style=\"font:700 16px/1.4 Segoe UI,Arial,sans-serif;color:#0f172a;\">{Enc(trainingTitle)}</div>" +
            (paraArchivo ? $"<div style=\"margin-top:8px;\"><span style=\"color:#64748b;\">Empleado:</span> <b>{Enc(learnerName)}</b></div>" : "") +
            $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Emitido:</span> <b>{issuedAt.ToLocalTime():dd/MM/yyyy}</b></div>" +
            (expiresAt is DateTime v ? $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Vigente hasta:</span> <b>{v.ToLocalTime():dd/MM/yyyy}</b></div>" : "") +
            $"<div style=\"margin-top:6px;\"><span style=\"color:#64748b;\">Folio:</span> <b style=\"font-family:Consolas,monospace;\">{Enc(serial)}</b></div>";
    }

    // Variante por enlace: sin adjunto, botón «Ver certificado» y aviso de cuánto vale el enlace.
    private static string CertificateIssuedLink(string learnerName, string trainingTitle, string serial,
        DateTime issuedAt, DateTime? expiresAt, bool paraArchivo, string? appUrl, string link, int dias)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var datos = DatosCertificado(learnerName, trainingTitle, serial, issuedAt, expiresAt, paraArchivo);
        var plazo = dias > 0 ? $"{dias} día{(dias == 1 ? "" : "s")}" : "un tiempo limitado";

        var body = paraArchivo
            ? Titulo("Certificado para expediente") +
              $"<p style=\"margin:0;\"><b>{Enc(learnerName)}</b> aprobó <b>{Enc(trainingTitle)}</b>. " +
              "Abre el certificado en PDF con el botón para archivarlo en su expediente.</p>" +
              Recuadro(datos) +
              Boton("Ver certificado", link) +
              $"<p style=\"margin:14px 0 0;color:#64748b;font-size:13px;\">El enlace vale {plazo}; después, el certificado " +
              "sigue disponible en el expediente del empleado dentro de Aprendor.</p>"
            : Titulo("¡Felicidades! Aquí está tu certificado") +
              $"<p style=\"margin:0 0 4px;\">Hola {Enc(learnerName)},</p>" +
              "<p style=\"margin:0;\">Completaste tu adiestramiento. Abre tu certificado en PDF con el botón para verlo, " +
              "descargarlo o imprimirlo.</p>" +
              Recuadro(datos) +
              Boton("Ver certificado", link) +
              $"<p style=\"margin:14px 0 0;color:#64748b;font-size:13px;\">El enlace vale {plazo}; después, lo encuentras " +
              "en tu expediente dentro de Aprendor.</p>";

        if (!string.IsNullOrWhiteSpace(appUrl))
            body += $"<p style=\"margin:6px 0 0;color:#64748b;font-size:13px;\">" +
                    $"<a href=\"{Enc(appUrl!)}\" style=\"color:#4f46e5;\">{(paraArchivo ? "Entrar a Aprendor" : "Ver mi expediente")}</a></p>";

        return Render(body,
            paraArchivo ? $"Certificado: {learnerName} aprobó {trainingTitle}" : $"Tu certificado: {trainingTitle}");
    }

    // Resumen para el oficial de cumplimiento. Secciones: vencidos nuevos desde el
    // último aviso, los que siguen vencidos (se repiten cada ExpiredRepeatDays días),
    // por vencer (ventana dueSoonDays) y sin comenzar. Las secciones vacías no salen.
    public static string ComplianceDigest(string companyName, List<ComplianceRow> nuevos, List<ComplianceRow> siguen,
        List<ComplianceRow> porVencer, List<ComplianceRow> sinComenzar, int dueSoonDays, string? appUrl)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var vencidos = nuevos.Count + siguen.Count;

        string Cifra(int n, string etiqueta, string color)
            => "<td align=\"center\" style=\"padding:12px 6px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;\">" +
               $"<div style=\"font:700 24px/1.1 Segoe UI,Arial,sans-serif;color:{color};\">{n}</div>" +
               $"<div style=\"font:400 12px/1.4 Segoe UI,Arial,sans-serif;color:#64748b;margin-top:4px;\">{etiqueta}</div></td>";

        // Tabla de una sección. Se corta en 50 filas para que el correo no se vuelva inmanejable.
        string Seccion(string titulo, string color, string colFecha, List<ComplianceRow> filas)
        {
            if (filas.Count == 0) return "";
            const int max = 50;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<div style=\"font:700 15px/1.4 Segoe UI,Arial,sans-serif;color:{color};margin:22px 0 8px;\">{Enc(titulo)} ({filas.Count})</div>");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" " +
                      "style=\"border:1px solid #e2e8f0;border-radius:10px;border-collapse:separate;font:400 13px/1.45 Segoe UI,Arial,sans-serif;color:#334155;\">");
            sb.Append("<tr style=\"background:#f8fafc;color:#64748b;font-size:12px;\">" +
                      "<td style=\"padding:8px 10px;\">Empleado</td><td style=\"padding:8px 10px;\">Grupo</td>" +
                      $"<td style=\"padding:8px 10px;\">Curso</td><td style=\"padding:8px 10px;\">{Enc(colFecha)}</td>" +
                      "<td style=\"padding:8px 10px;\" align=\"right\">Días</td></tr>");
            foreach (var f in filas.Take(max))
            {
                var grupo = f.Groups.Count > 0 ? string.Join(", ", f.Groups) : "—";
                var fecha = f.Date is DateTime d ? d.ToString("dd/MM/yyyy") : "—";
                var dias = f.Days is int n ? n.ToString() : "—";
                sb.Append("<tr>" +
                          $"<td style=\"padding:8px 10px;border-top:1px solid #e2e8f0;\"><b>{Enc(f.Name)}</b></td>" +
                          $"<td style=\"padding:8px 10px;border-top:1px solid #e2e8f0;\">{Enc(grupo)}</td>" +
                          $"<td style=\"padding:8px 10px;border-top:1px solid #e2e8f0;\">{Enc(f.Title)}</td>" +
                          $"<td style=\"padding:8px 10px;border-top:1px solid #e2e8f0;white-space:nowrap;\">{fecha}</td>" +
                          $"<td style=\"padding:8px 10px;border-top:1px solid #e2e8f0;\" align=\"right\">{dias}</td></tr>");
            }
            sb.Append("</table>");
            if (filas.Count > max)
                sb.Append($"<div style=\"font:400 12px/1.5 Segoe UI,Arial,sans-serif;color:#64748b;margin-top:6px;\">…y {filas.Count - max} más. Ve la lista completa en Aprendor.</div>");
            return sb.ToString();
        }

        var body =
            Titulo("Resumen de cumplimiento") +
            $"<p style=\"margin:0 0 4px;\">Estado de los adiestramientos obligatorios en <b>{Enc(companyName)}</b>.</p>" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"6\" border=\"0\" style=\"margin:14px 0 4px;\"><tr>" +
            Cifra(vencidos, "Vencidos", "#b91c1c") +
            Cifra(porVencer.Count, $"Por vencer ({dueSoonDays} días)", "#b45309") +
            Cifra(sinComenzar.Count, "Sin comenzar", "#475569") +
            "</tr></table>" +
            Seccion("Nuevos desde el último aviso", "#b91c1c", "Venció", nuevos) +
            Seccion("Siguen vencidos", "#991b1b", "Venció", siguen) +
            Seccion("Por vencer", "#b45309", "Vence", porVencer) +
            Seccion("Sin comenzar", "#475569", "Fecha límite", sinComenzar) +
            "<p style=\"margin:18px 0 0;color:#64748b;font-size:13px;\">En «Vencidos» los días son los que lleva vencido; " +
            "en las demás secciones, los que faltan para la fecha.</p>" +
            Boton("Ver en Aprendor", appUrl ?? "");
        return Render(body, $"Cumplimiento: {vencidos} vencidos, {porVencer.Count} por vencer.");
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
            var ingresos = await CatalogLogic.FechasIngresoAsync(catalog, t.Id);

            foreach (var u in users)
            {
                var cohortIds = await db.UserGroupMembers.Where(m => m.UserId == u.Id)
                    .Select(m => m.UserGroupId).ToListAsync();
                var pending = await CatalogLogic.ResolveAsync(db, u.Id, cohortIds,
                    ingresos.TryGetValue(u.Id, out var ingreso) ? ingreso : null);
                var notStarted = pending.Where(p => p.Status == "not-started").ToList();
                var inProgress = pending.Where(p => p.Status == "in-progress" || p.Status == "failed"
                                                 || p.Status == "renewal" || p.Status == "expired"
                                                 || p.Status == "overdue").ToList();
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
            var ingresos = await CatalogLogic.FechasIngresoAsync(catalog, tenant.Id);

            var opts = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(tenant.ConnectionString).Options;
            await using var db = new TenantDbContext(opts);

            var hoy = DateTime.UtcNow;
            foreach (var u in usuarios)
            {
                List<PendingItem> pendientes;
                try
                {
                    var grupos = await db.UserGroupMembers.Where(m => m.UserId == u.Id)
                        .Select(m => m.UserGroupId).ToListAsync();
                    pendientes = await CatalogLogic.ResolveAsync(db, u.Id, grupos,
                        ingresos.TryGetValue(u.Id, out var ingreso) ? ingreso : null);
                }
                catch { continue; }

                foreach (var p in pendientes)
                {
                    // Solo lo que el learner puede tomar ahora y no ha completado.
                    var tomable = p.Status is "not-started" or "renewal" or "expired" or "failed" or "overdue";
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
                    // Sin caducidad de una aprobación previa, cuenta la fecha límite del plan (DueAt).
                    var esLimite = p.ExpiresAt is null && p.DueAt is not null;
                    if ((p.ExpiresAt ?? p.DueAt) is DateTime vence && reglas.DaysBefore.Count > 0)
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
                                EmailTemplates.CourseReminder(u.Name, p.Title, tipo, p.ExpiresAt ?? p.DueAt, appUrl, esLimite));
                            enviados++;
                        }
                        catch { /* el registro ya quedó; no se reintenta para no spamear */ }
                    }

                    // Vencido (expired / overdue): aviso `overdue:{sello}` que se repite cada
                    // OverdueEveryDays días mientras siga vencido (0 = apagado).
                    if (reglas.OverdueEveryDays > 0 && OverdueReminder.FechaVencida(p) is DateTime vencio
                        && await OverdueReminder.ReservarAsync(db, u.Id, p.TrainingId, vencio, reglas.OverdueEveryDays))
                    {
                        try
                        {
                            await OverdueReminder.EnviarAsync(email, u.Email, u.Name, p, vencio, appUrl);
                            enviados++;
                        }
                        catch { /* la reserva ya quedó; el próximo sale en N días */ }
                    }
                }
            }
        }
        return enviados;
    }
}

// ---- Aviso al empleado con un curso vencido ----
// Idempotencia con NotificationLog (UserId, TrainingId, Kind = overdue:{yyyyMMdd}). A
// diferencia de los demás avisos, la fila se RENUEVA (SentAt) cada N días mientras el
// curso siga vencido. El sello es la fecha de caducidad (expired) o la fecha límite
// (overdue): si cambia (nuevo plazo, nueva aprobación que vuelve a vencer) es otro aviso.
public static class OverdueReminder
{
    // La fecha que venció, o null si el curso no está vencido.
    public static DateTime? FechaVencida(PendingItem p) => p.Status switch
    {
        "expired" => p.ExpiresAt,
        "overdue" => p.DueAt,
        _ => null
    };

    public static string Kind(DateTime vencio) => $"overdue:{vencio:yyyyMMdd}";

    // Reserva el envío: true si toca mandarlo ahora (fila nueva, o el último hace
    // `cadaDias` días o más). Con `forzar` (botón «Recordar ahora») se reserva siempre
    // y la cadencia vuelve a contar desde hoy.
    public static async Task<bool> ReservarAsync(TenantDbContext db, Guid userId, Guid trainingId, DateTime vencio,
        int cadaDias, bool forzar = false)
    {
        var kind = Kind(vencio);
        var ahora = DateTime.UtcNow;
        var fila = await db.NotificationLogs.AsNoTracking()
            .FirstOrDefaultAsync(n => n.UserId == userId && n.TrainingId == trainingId && n.Kind == kind);
        if (fila is null)
        {
            var nueva = new NotificationLog { UserId = userId, TrainingId = trainingId, Kind = kind, SentAt = ahora };
            db.NotificationLogs.Add(nueva);
            try { await db.SaveChangesAsync(); return true; }
            catch (DbUpdateException)
            {
                // Otro proceso la creó primero: ese ya lo mandó.
                db.Entry(nueva).State = EntityState.Detached;
                return false;
            }
        }
        if (!forzar && fila.SentAt > ahora.AddDays(-Math.Max(1, cadaDias))) return false;

        // Actualización condicionada: si otro proceso la renovó entretanto, no se repite.
        var cambiadas = await db.NotificationLogs.Where(n => n.Id == fila.Id && n.SentAt == fila.SentAt)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.SentAt, ahora));
        return cambiadas > 0 || forzar;
    }

    public static Task EnviarAsync(IEmailSender email, string toEmail, string toName, PendingItem p, DateTime vencio, string? appUrl)
        => email.SendAsync(toEmail, toName, $"Vencido: {p.Title}",
            EmailTemplates.CourseReminder(toName, p.Title, "overdue", vencio, appUrl, esLimite: p.Status == "overdue"));
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
    // (También la usa ComplianceDigestService con la cadencia de cada compañía.)
    internal static bool TryDigestKey(DateTime now, string freq, int hour,
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

// ============================================================================
//  Cumplimiento: estado de toda la compañía (panel del oficial y su resumen)
// ============================================================================

// Una persona y un curso en el panel o el resumen de cumplimiento.
//   Bucket:   overdue (vencido: expired u overdue) | due-soon (por vencer) | not-started |
//             current (al día: aprobado y vigente, lejos de vencer o sin vencimiento)
//   DateKind: expires (caducidad de una aprobación) | due (fecha límite del plan) | null
//   Days:     en overdue, los días que lleva vencido; en los demás, los que faltan.
//   ApprovedAt: última aprobación (si la hay). CertificateSerial: folio del último
//   certificado vigente o, si ninguno lo está, del último emitido (para abrirlo desde
//   las listas); CertificateExpired indica ese segundo caso.
public record ComplianceRow(Guid UserId, string Name, string Email, List<Guid> GroupIds, List<string> Groups,
    Guid TrainingId, string Title, string Status, string Bucket, DateTime? Date, string? DateKind, int? Days,
    DateTime? ApprovedAt = null, string? CertificateSerial = null, bool CertificateExpired = false);

public static class ComplianceState
{
    // En qué lista cae un pendiente; null = no cuenta (en curso o reprobado sin fecha
    // cerca, esperando calificación o cancelación). Las listas no se pisan: un curso vencido
    // no sale también como por vencer, ni uno por vencer como sin comenzar o al día.
    public static (string bucket, DateTime? fecha, string? tipo)? Clasificar(PendingItem p, DateTime ahora, int dueSoonDays)
    {
        if (p.Status == "expired") return ("overdue", p.ExpiresAt, "expires");
        if (p.Status == "overdue") return ("overdue", p.DueAt, "due");

        var fecha = p.ExpiresAt ?? p.DueAt;
        var tipo = p.ExpiresAt is not null ? "expires" : p.DueAt is not null ? "due" : null;
        if (p.Status is "current" or "renewal" or "not-started" or "in-progress" or "failed"
            && fecha is DateTime f && f >= ahora && f <= ahora.AddDays(dueSoonDays))
            return ("due-soon", fecha, tipo);

        if (p.Status == "not-started") return ("not-started", p.DueAt, p.DueAt is not null ? "due" : null);
        // Al día: aprobado y vigente fuera de la ventana de «por vencer» (renewal sigue
        // vigente: solo se reabrió para renovar antes de tiempo).
        if (p.Status is "current" or "done" or "renewal")
            return ("current", p.ExpiresAt, p.ExpiresAt is not null ? "expires" : null);
        return null;
    }

    // Estado de cada miembro de la compañía en cada curso que le toca (mismo resolver
    // que ve el empleado, con sus grupos y su fecha de ingreso). conCertificados: añade a
    // cada fila la fecha de la última aprobación y el folio del último certificado (panel);
    // el resumen por correo no lo necesita y se ahorra esas consultas.
    public static async Task<List<ComplianceRow>> ResolverAsync(CatalogDbContext catalog, TenantDbContext db,
        Guid tenantId, int dueSoonDays, bool conCertificados = false)
    {
        var usuarios = await CompanyUsers.OfAsync(catalog, tenantId);
        var ingresos = await CatalogLogic.FechasIngresoAsync(catalog, tenantId);
        var membresias = await db.UserGroupMembers.AsNoTracking()
            .Select(m => new { m.UserId, m.UserGroupId }).ToListAsync();
        var nombres = await db.UserGroups.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name);
        var ahora = DateTime.UtcNow;

        // Última aprobación por persona y curso (la misma que usa el resolver: el intento
        // aprobado más reciente) y certificados emitidos, del más nuevo al más viejo.
        var aprobaciones = new Dictionary<(Guid, Guid), DateTime>();
        var certificados = new Dictionary<(Guid, Guid), List<(string Serial, DateTime? Expira)>>();
        if (conCertificados)
        {
            var aprobados = await (from a in db.Attempts.AsNoTracking()
                                   where a.Passed && a.CompletedAt != null && a.UserId != null
                                   join v in db.TrainingVersions.AsNoTracking() on a.TrainingVersionId equals v.Id
                                   select new { UserId = a.UserId!.Value, v.TrainingId, Fecha = a.CompletedAt!.Value })
                                  .ToListAsync();
            foreach (var a in aprobados)
                if (!aprobaciones.TryGetValue((a.UserId, a.TrainingId), out var f) || a.Fecha > f)
                    aprobaciones[(a.UserId, a.TrainingId)] = a.Fecha;

            var emitidos = await db.Certificates.AsNoTracking().Where(c => c.UserId != null)
                .OrderByDescending(c => c.IssuedAt)
                .Select(c => new { UserId = c.UserId!.Value, c.TrainingId, c.Serial, c.ExpiresAt })
                .ToListAsync();
            foreach (var c in emitidos)
            {
                if (!certificados.TryGetValue((c.UserId, c.TrainingId), out var l))
                    certificados[(c.UserId, c.TrainingId)] = l = new();
                l.Add((c.Serial, c.ExpiresAt));
            }
        }

        var filas = new List<ComplianceRow>();
        foreach (var u in usuarios.OrderBy(x => x.Name))
        {
            var grupos = membresias.Where(m => m.UserId == u.Id).Select(m => m.UserGroupId).Distinct().ToList();
            List<PendingItem> pendientes;
            try
            {
                pendientes = await CatalogLogic.ResolveAsync(db, u.Id, grupos,
                    ingresos.TryGetValue(u.Id, out var ingreso) ? ingreso : null);
            }
            catch { continue; }

            var nombresGrupos = grupos.Where(nombres.ContainsKey).Select(g => nombres[g]).OrderBy(n => n).ToList();
            foreach (var p in pendientes)
            {
                if (Clasificar(p, ahora, dueSoonDays) is not { } c) continue;
                int? dias = c.fecha is DateTime f
                    ? (c.bucket == "overdue" ? (ahora.Date - f.Date).Days : (f.Date - ahora.Date).Days)
                    : null;
                DateTime? aprobado = aprobaciones.TryGetValue((u.Id, p.TrainingId), out var fa) ? fa : null;
                string? folio = null; var caducado = false;
                if (certificados.TryGetValue((u.Id, p.TrainingId), out var certs))
                {
                    var vigente = certs.FirstOrDefault(x => x.Expira is null || x.Expira > ahora);
                    if (vigente.Serial is not null) folio = vigente.Serial;
                    else { folio = certs[0].Serial; caducado = true; }
                }
                filas.Add(new ComplianceRow(u.Id, u.Name, u.Email, grupos, nombresGrupos,
                    p.TrainingId, p.Title, p.Status, c.bucket, c.fecha, c.tipo, dias,
                    aprobado, folio, caducado));
            }
        }
        return filas;
    }

    public static object ToJson(ComplianceRow r) => new
    {
        userId = r.UserId, name = r.Name, email = r.Email, groupIds = r.GroupIds, groups = r.Groups,
        trainingId = r.TrainingId, title = r.Title, status = r.Status, bucket = r.Bucket,
        date = r.Date, dateKind = r.DateKind, days = r.Days,
        approvedAt = r.ApprovedAt, certificateSerial = r.CertificateSerial, certificateExpired = r.CertificateExpired
    };
}

// Quién puede ver el panel de cumplimiento: el Admin de la compañía activa o un
// oficial de cumplimiento marcado en ella (la marca no es un rol, se consulta aquí).
public static class ComplianceAccess
{
    public static async Task<bool> PuedeVerAsync(CatalogDbContext catalog, ITenantContext tc)
        => tc.TenantId is not null
           && (tc.Role == "Admin" || await ComplianceOfficers.EsOficialAsync(catalog, tc.UserId, tc.TenantId));

    public static Task<bool> EsOficialAsync(CatalogDbContext catalog, ITenantContext tc)
        => ComplianceOfficers.EsOficialAsync(catalog, tc.UserId, tc.TenantId);
}

// ---- Resumen para el oficial de cumplimiento ----
// Destinatarios: oficiales marcados ∪ ComplianceConfig.extraEmails; si no hay ninguno,
// la compañía se salta. Idempotencia de los vencidos con NotificationLog del tenant:
// Kind = officer-expired:{trainingId}:{yyyyMMdd de vencimiento o límite}, UserId = el
// empleado. Sin fila -> «Nuevos» (se crea); fila con SentAt de hace ExpiredRepeatDays
// días o más -> «Siguen vencidos» (se renueva SentAt); reciente -> no sale.
public static class ComplianceDigestRunner
{
    public record Resultado(Guid TenantId, string Tenant, int Destinatarios, int Enviados,
        int Nuevos, int Siguen, int PorVencer, int SinComenzar, string? Omitido = null);

    public const string KindPrefix = "officer-expired:";

    // Todas las compañías activas (o solo una), sin mirar la cadencia: eso lo decide
    // quien llama (el servicio en segundo plano o el endpoint de prueba).
    public static async Task<List<Resultado>> RunAsync(IServiceProvider sp, IEmailSender email, IConfiguration cfg,
        Guid? soloTenant = null)
    {
        using var scope = sp.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var q = catalog.Tenants.Where(t => t.Status == "active");
        if (soloTenant is Guid id) q = q.Where(t => t.Id == id);
        var tenants = await q.ToListAsync();

        var lista = new List<Resultado>();
        foreach (var t in tenants)
        {
            try { lista.Add(await RunTenantAsync(catalog, email, t, cfg["App:BaseUrl"])); }
            catch { lista.Add(new Resultado(t.Id, t.Name, 0, 0, 0, 0, 0, 0, "error")); }
        }
        return lista;
    }

    // Una compañía. Con `prueba` el resumen va SOLO a esa persona, sale aunque esté
    // vacío y no toca NotificationLog: los vencidos nuevos siguen siendo nuevos para el
    // resumen de verdad (y los ya avisados salen como «Siguen vencidos»).
    public static async Task<Resultado> RunTenantAsync(CatalogDbContext catalog, IEmailSender email, Tenant tenant,
        string? appUrl, CompanyUsers.Miembro? prueba = null)
    {
        var comp = ComplianceConfig.Parse(tenant.ComplianceConfigJson);

        var destinos = new List<(string Email, string Name)>();
        void Sumar(string? correo, string nombre)
        {
            var c = (correo ?? "").Trim();
            if (c.Length == 0 || !c.Contains('@')) return;
            if (destinos.Any(d => string.Equals(d.Email, c, StringComparison.OrdinalIgnoreCase))) return;
            destinos.Add((c, nombre));
        }
        if (prueba is not null) Sumar(prueba.Email, prueba.Name);
        else
        {
            foreach (var o in await ComplianceOfficers.OfAsync(catalog, tenant.Id)) Sumar(o.Email, o.Name);
            foreach (var c in comp.ExtraEmails) Sumar(c, "");
        }
        if (destinos.Count == 0)
            return new Resultado(tenant.Id, tenant.Name, 0, 0, 0, 0, 0, 0, "sin destinatarios");
        if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
            return new Resultado(tenant.Id, tenant.Name, destinos.Count, 0, 0, 0, 0, 0, "sin base de datos");

        var opts = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(tenant.ConnectionString).Options;
        await using var db = new TenantDbContext(opts);

        var filas = await ComplianceState.ResolverAsync(catalog, db, tenant.Id, comp.DueSoonDays);
        var ahora = DateTime.UtcNow;

        // Vencidos: nuevos o que ya toca repetir.
        var registros = (await db.NotificationLogs.Where(n => n.Kind.StartsWith(KindPrefix)).ToListAsync())
            .GroupBy(n => (n.UserId, n.TrainingId, n.Kind)).ToDictionary(g => g.Key, g => g.First());
        var nuevos = new List<ComplianceRow>();
        var siguen = new List<ComplianceRow>();
        foreach (var f in filas.Where(f => f.Bucket == "overdue" && f.Date is not null).OrderBy(f => f.Date))
        {
            var kind = $"{KindPrefix}{f.TrainingId}:{f.Date:yyyyMMdd}";
            if (!registros.TryGetValue((f.UserId, f.TrainingId, kind), out var log))
            {
                nuevos.Add(f);
                if (prueba is null)
                {
                    var fila = new NotificationLog { UserId = f.UserId, TrainingId = f.TrainingId, Kind = kind, SentAt = ahora };
                    db.NotificationLogs.Add(fila);
                    registros[(f.UserId, f.TrainingId, kind)] = fila;
                }
            }
            else if (prueba is not null || log.SentAt <= ahora.AddDays(-comp.ExpiredRepeatDays))
            {
                siguen.Add(f);
                if (prueba is null) log.SentAt = ahora;
            }
        }

        var porVencer = filas.Where(f => f.Bucket == "due-soon").OrderBy(f => f.Date).ThenBy(f => f.Name).ToList();
        var sinComenzar = comp.IncludeNotStarted
            ? filas.Where(f => f.Bucket == "not-started").OrderBy(f => f.Date ?? DateTime.MaxValue).ThenBy(f => f.Name).ToList()
            : new List<ComplianceRow>();

        // Solo se envía si hay algo que contar (salvo la prueba, que muestra el formato).
        if (prueba is null && nuevos.Count + siguen.Count + porVencer.Count + sinComenzar.Count == 0)
            return new Resultado(tenant.Id, tenant.Name, destinos.Count, 0, 0, 0, 0, 0, "nada que avisar");

        var vencidos = nuevos.Count + siguen.Count;
        var asunto = $"Cumplimiento: {vencidos} vencidos, {porVencer.Count} por vencer";
        if (prueba is not null) asunto = "[Prueba] " + asunto;
        var html = EmailTemplates.ComplianceDigest(tenant.Name, nuevos, siguen, porVencer, sinComenzar, comp.DueSoonDays, appUrl);

        int enviados = 0;
        foreach (var d in destinos)
        {
            try { await email.SendAsync(d.Email, d.Name, asunto, html); enviados++; }
            catch { /* seguir con los demás */ }
        }

        // Si no salió ningún correo no se registra nada: el próximo resumen los vuelve a traer.
        if (prueba is null && enviados > 0)
        {
            db.AuditLogs.Add(new TenantAuditLog
            {
                Action = "compliance-digest",
                Detail = $"{enviados} destinatario(s): {vencidos} vencidos ({nuevos.Count} nuevos), " +
                         $"{porVencer.Count} por vencer, {sinComenzar.Count} sin comenzar"
            });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { /* otra corrida registró primero */ }
        }

        return new Resultado(tenant.Id, tenant.Name, destinos.Count, enviados,
            nuevos.Count, siguen.Count, porVencer.Count, sinComenzar.Count);
    }
}

// Corre el resumen de cumplimiento de cada compañía con SU cadencia
// (ComplianceConfig.digestFrequency / DayOfWeek / DayOfMonth / Hour). Revisa cada
// 30 minutos; el marcador App_Data/compliance_digest/{tenant}.txt guarda el último
// periodo enviado, así cada compañía recibe uno por periodo aunque la app reinicie.
// Encendido por defecto (sin oficiales ni correos extra no sale nada); se apaga con
// Email:ComplianceDigestEnabled = false.
public class ComplianceDigestService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;

    public ComplianceDigestService(IServiceProvider sp, IEmailSender email, IConfiguration cfg, IHostEnvironment env)
    { _sp = sp; _email = email; _cfg = cfg; _env = env; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (bool.TryParse(_cfg["Email:ComplianceDigestEnabled"], out var en) && !en) return;

        var carpeta = Path.Combine(_env.ContentRootPath, "App_Data", "compliance_digest");
        try { Directory.CreateDirectory(carpeta); } catch { }

        try { await Task.Delay(TimeSpan.FromMinutes(3), ct); }   // deja arrancar la app
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await CicloAsync(carpeta, ct); }
            catch (OperationCanceledException) { break; }
            catch { /* reintentar en el próximo ciclo */ }

            try { await Task.Delay(TimeSpan.FromMinutes(30), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CicloAsync(string carpeta, CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var tenants = await catalog.Tenants.Where(t => t.Status == "active").ToListAsync(ct);
        var ahora = DateTime.Now;

        foreach (var t in tenants)
        {
            var comp = ComplianceConfig.Parse(t.ComplianceConfigJson);
            var dia = Enum.TryParse<DayOfWeek>(comp.DigestDayOfWeek, true, out var d) ? d : DayOfWeek.Monday;
            if (!WeeklyDigestService.TryDigestKey(ahora, comp.DigestFrequency, comp.DigestHour, dia, comp.DigestDayOfMonth, out var key))
                continue;

            var marcador = Path.Combine(carpeta, $"{t.Id:N}.txt");
            var ultimo = File.Exists(marcador) ? (await File.ReadAllTextAsync(marcador, ct)).Trim() : "";
            if (ultimo == key) continue;

            try { await ComplianceDigestRunner.RunTenantAsync(catalog, _email, t, _cfg["App:BaseUrl"]); }
            catch { continue; }   // sin marcador: se reintenta en el próximo ciclo
            await File.WriteAllTextAsync(marcador, key, ct);
        }
    }
}

// ============================================================================
//  Endpoints de cumplimiento (Admin de la compañía u oficial de cumplimiento)
// ============================================================================
public static class ComplianceEndpoints
{
    // Freno para «Recordar ahora»: un aviso por persona y curso por minuto.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid, Guid, Guid), DateTime> _ultimos = new();

    public static void MapCompliance(this WebApplication app)
    {
        // ---- Panel: vencidos, por vencer, sin comenzar y al día, con nombre, grupo, curso, fecha y días ----
        // (y en cada fila la última aprobación y el folio del último certificado, si lo hay)
        // Filtros opcionales: ?trainingId=…&groupId=…
        app.MapGet("/compliance/summary", async (Guid? trainingId, Guid? groupId, ITenantContext tc,
            IServiceProvider sp, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!await ComplianceAccess.PuedeVerAsync(catalog, tc)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();
            var tenant = await catalog.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tc.TenantId);
            var comp = ComplianceConfig.Parse(tenant?.ComplianceConfigJson);

            var filas = await ComplianceState.ResolverAsync(catalog, db, tc.TenantId.Value, comp.DueSoonDays,
                conCertificados: true);
            if (trainingId is Guid tid) filas = filas.Where(f => f.TrainingId == tid).ToList();
            if (groupId is Guid gid) filas = filas.Where(f => f.GroupIds.Contains(gid)).ToList();

            var vencidos = filas.Where(f => f.Bucket == "overdue").OrderByDescending(f => f.Days).ThenBy(f => f.Name).ToList();
            var porVencer = filas.Where(f => f.Bucket == "due-soon").OrderBy(f => f.Date).ThenBy(f => f.Name).ToList();
            var sinComenzar = filas.Where(f => f.Bucket == "not-started")
                .OrderBy(f => f.Date ?? DateTime.MaxValue).ThenBy(f => f.Name).ToList();
            // Al día: aprobado y vigente (fecha = vigente hasta; null = sin vencimiento).
            var alDia = filas.Where(f => f.Bucket == "current").OrderBy(f => f.Name).ThenBy(f => f.Title).ToList();

            return Results.Ok(new
            {
                generatedAt = DateTime.UtcNow,
                dueSoonDays = comp.DueSoonDays,
                counts = new { overdue = vencidos.Count, dueSoon = porVencer.Count, notStarted = sinComenzar.Count, current = alDia.Count },
                overdue = vencidos.Select(ComplianceState.ToJson),
                dueSoon = porVencer.Select(ComplianceState.ToJson),
                notStarted = sinComenzar.Select(ComplianceState.ToJson),
                current = alDia.Select(ComplianceState.ToJson)
            });
        }).RequireAuthorization();

        // ---- Alertas por curso: cuántos vencidos tiene y desde cuándo el más antiguo ----
        app.MapGet("/compliance/alerts", async (ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!await ComplianceAccess.PuedeVerAsync(catalog, tc)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();
            var tenant = await catalog.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tc.TenantId);
            var comp = ComplianceConfig.Parse(tenant?.ComplianceConfigJson);

            var filas = await ComplianceState.ResolverAsync(catalog, db, tc.TenantId.Value, comp.DueSoonDays);
            var cursos = filas.GroupBy(f => new { f.TrainingId, f.Title })
                .Select(g => new
                {
                    trainingId = g.Key.TrainingId,
                    title = g.Key.Title,
                    overdue = g.Count(f => f.Bucket == "overdue"),
                    oldest = g.Where(f => f.Bucket == "overdue").Min(f => f.Date),
                    dueSoon = g.Count(f => f.Bucket == "due-soon")
                })
                .Where(c => c.overdue > 0)
                .OrderByDescending(c => c.overdue).ThenBy(c => c.oldest)
                .ToList();

            return Results.Ok(new { totalOverdue = cursos.Sum(c => c.overdue), courses = cursos });
        }).RequireAuthorization();

        // ---- «Recordar ahora»: aviso inmediato al empleado, saltando la cadencia ----
        // Vencido: variante «vencido» y la cadencia de OverdueEveryDays vuelve a contar
        // desde hoy. Pendiente sin vencer: recordatorio con los días que le quedan.
        app.MapPost("/compliance/remind/{userId:guid}/{trainingId:guid}", async (Guid userId, Guid trainingId,
            ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog, IEmailSender email, IConfiguration config) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!await ComplianceAccess.PuedeVerAsync(catalog, tc)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();

            var persona = (await CompanyUsers.OfAsync(catalog, tc.TenantId.Value)).FirstOrDefault(u => u.Id == userId);
            if (persona is null) return Results.NotFound("La persona no pertenece a esta compañía.");
            if (string.IsNullOrWhiteSpace(persona.Email)) return Results.BadRequest("La persona no tiene un correo registrado.");

            var grupos = await db.UserGroupMembers.Where(m => m.UserId == userId).Select(m => m.UserGroupId).ToListAsync();
            var ingreso = await CatalogLogic.FechaIngresoAsync(catalog, userId, tc.TenantId);
            var p = (await CatalogLogic.ResolveAsync(db, userId, grupos, ingreso)).FirstOrDefault(x => x.TrainingId == trainingId);
            if (p is null) return Results.NotFound("Ese curso no le toca a esta persona.");
            if (p.Status is "current" or "done" or "pending-grading" or "pending-cancellation")
                return Results.BadRequest("No tiene nada pendiente en este curso.");

            var clave = (tc.TenantId.Value, userId, trainingId);
            var ahora = DateTime.UtcNow;
            if (_ultimos.TryGetValue(clave, out var antes) && ahora - antes < TimeSpan.FromMinutes(1))
                return Results.Json("Se acaba de enviar; espera un minuto antes de repetirlo.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            _ultimos[clave] = ahora;

            var appUrl = config["App:BaseUrl"];
            string tipo;
            try
            {
                if (OverdueReminder.FechaVencida(p) is DateTime vencio)
                {
                    tipo = "overdue";
                    var reglas = NotificationConfig.Parse(await db.Trainings.Where(t => t.Id == trainingId)
                        .Select(t => t.NotificationConfigJson).FirstOrDefaultAsync());
                    await OverdueReminder.ReservarAsync(db, userId, trainingId, vencio, reglas.OverdueEveryDays, forzar: true);
                    await OverdueReminder.EnviarAsync(email, persona.Email, persona.Name, p, vencio, appUrl);
                }
                else
                {
                    var fecha = p.ExpiresAt ?? p.DueAt;
                    var dias = fecha is DateTime f ? (f.Date - ahora.Date).Days : -1;
                    tipo = dias >= 0 ? $"due{dias}" : "open";
                    await email.SendAsync(persona.Email, persona.Name, $"Recordatorio: {p.Title}",
                        EmailTemplates.CourseReminder(persona.Name, p.Title, tipo, fecha, appUrl,
                            esLimite: p.ExpiresAt is null && p.DueAt is not null));
                }
            }
            catch
            {
                _ultimos.TryRemove(clave, out _);
                return Results.Json(new { sent = false, error = "No se pudo enviar el correo." },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            db.AuditLogs.Add(new TenantAuditLog
            {
                Action = "compliance-remind",
                Detail = $"{p.Title} -> {persona.Email} ({p.Status})",
                UserId = tc.UserId
            });
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                sent = true, userId, trainingId, title = p.Title, status = p.Status,
                email = persona.Email, kind = tipo == "overdue" ? "overdue" : "reminder"
            });
        }).RequireAuthorization();

        // ---- Correo de prueba del resumen: solo a quien lo pide, sin tocar el registro ----
        app.MapPost("/company/compliance/test", async (ITenantContext tc, CatalogDbContext catalog,
            IEmailSender email, IConfiguration config) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!await ComplianceAccess.PuedeVerAsync(catalog, tc)) return Results.Forbid();
            var tenant = await catalog.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tc.TenantId);
            var yo = await catalog.Users.AsNoTracking().Where(u => u.Id == tc.UserId)
                .Select(u => new CompanyUsers.Miembro(u.Id, u.Email, u.Name, u.Role)).FirstOrDefaultAsync();
            if (tenant is null || yo is null) return Results.NotFound();

            var r = await ComplianceDigestRunner.RunTenantAsync(catalog, email, tenant, config["App:BaseUrl"], prueba: yo);
            return Results.Ok(new
            {
                sent = r.Enviados > 0, to = yo.Email,
                overdue = r.Nuevos + r.Siguen, dueSoon = r.PorVencer, notStarted = r.SinComenzar
            });
        }).RequireAuthorization();
    }
}
