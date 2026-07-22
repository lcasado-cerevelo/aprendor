using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainingPlatform.Catalog;
using TrainingPlatform.TenantData;

namespace TrainingPlatform.Notifications;

// ---- Envío de correo (SMTP, sin dependencias externas) ----
public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}

// Usa el relay SMTP configurado en Email:* (p. ej. SendGrid, Office365).
// Si Email:Host no está configurado, no hace nada (la app corre igual).
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    public SmtpEmailSender(IConfiguration cfg) { _cfg = cfg; }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
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

        using var client = new SmtpClient(host, port) { EnableSsl = ssl };
        var user = _cfg["Email:User"];
        var pass = _cfg["Email:Password"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, pass);

        await client.SendMailAsync(msg);
    }
}

// ---- Resolución del catálogo / pendientes de un usuario ----
// Un curso "sale" mientras el usuario NO lo tenga aprobado y vigente. Estados:
// not-started | in-progress | pending-grading | pending-cancellation | failed |
// renewal (vigente pero reabierto para renovar) | expired (venció) |
// current (aprobado y vigente) | done (aprobado, sin caducidad).
public record PendingItem(Guid TrainingId, string Title, Guid VersionId, Guid? SetId, string Status, DateTime? ExpiresAt);

public static class CatalogLogic
{
    public static async Task<List<PendingItem>> ResolveAsync(TenantDbContext db, Guid? userId, List<Guid> cohortIds)
    {
        var published = await (from v in db.TrainingVersions
                               where v.Status == "published"
                               join t in db.Trainings on v.TrainingId equals t.Id
                               select new { trainingId = t.Id, t.Title, t.RecurrenceMonths, t.RenewLeadDays, versionId = v.Id, v.VersionNumber })
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
                    if (v.RecurrenceMonths is null) status = "done"; // una sola vez: aprobado para siempre
                    else
                    {
                        var expiry = lastPass.CompletedAt!.Value.AddMonths(v.RecurrenceMonths.Value);
                        var renewalOpen = expiry.AddDays(-Math.Max(0, v.RenewLeadDays));
                        expiresAt = expiry;
                        if (now < renewalOpen) status = "current";          // aprobado y vigente, aún no toca renovar
                        else status = now >= expiry ? "expired" : "renewal"; // reabierto para renovar / vencido
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
}

// ---- Plantillas HTML de correo ----
public static class EmailTemplates
{
    private const string Wrap =
        "<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden\">" +
        "<div style=\"background:#0b1220;color:#fff;padding:16px 20px;font-size:16px;font-weight:700;letter-spacing:.2px\">Aprendor</div>" +
        "<div style=\"padding:20px;color:#0f172a;font-size:14px;line-height:1.55\">{BODY}</div>" +
        "<div style=\"padding:12px 20px;background:#f8fafc;color:#64748b;font-size:12px\">Mensaje automático. No respondas a este correo.</div></div>";

    private static string Li(string title) => $"<li style=\"margin:4px 0\">{System.Net.WebUtility.HtmlEncode(title)}</li>";

    public static string Digest(string name, List<PendingItem> notStarted, List<PendingItem> inProgress)
    {
        var b = new System.Text.StringBuilder();
        b.Append($"<p>Hola {System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? "" : name)},</p>");
        b.Append("<p>Tienes adiestramientos pendientes:</p>");
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
        b.Append("<p>Entra a la plataforma para completarlos.</p>");
        return Wrap.Replace("{BODY}", b.ToString());
    }

    public static string Invitation(string name, string email, string tempPassword, string? appUrl)
    {
        string Enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        var btn = string.IsNullOrWhiteSpace(appUrl) ? "" :
            $"<p style=\"margin:22px 0 4px\"><a href=\"{Enc(appUrl)}\" style=\"display:inline-block;background:#4f46e5;color:#fff;text-decoration:none;padding:11px 22px;border-radius:8px;font-weight:700\">Entrar a Aprendor</a></p>";
        var body =
            $"<p>Hola {Enc(name)},</p>" +
            "<p>Te damos la bienvenida a <b>Aprendor</b>. Tu cuenta ya está lista para que comiences tus adiestramientos.</p>" +
            "<div style=\"background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;margin:14px 0\">" +
            $"<div style=\"margin:5px 0\"><span style=\"color:#64748b\">Usuario:</span> <b>{Enc(email)}</b></div>" +
            $"<div style=\"margin:5px 0\"><span style=\"color:#64748b\">Contraseña temporal:</span> <b>{Enc(tempPassword)}</b></div></div>" +
            "<p>Por seguridad, la primera vez que inicies sesión te pediremos crear tu propia contraseña.</p>" +
            btn;
        return Wrap.Replace("{BODY}", body);
    }

    public static string Completion(string learnerName, string trainingTitle, int score, int total)
    {
        var pct = total > 0 ? (int)Math.Round(score * 100.0 / total) : 100;
        var body =
            $"<p>El usuario <b>{System.Net.WebUtility.HtmlEncode(learnerName ?? "(sin nombre)")}</b> completó satisfactoriamente el adiestramiento:</p>" +
            $"<p style=\"font-size:16px;font-weight:600\">{System.Net.WebUtility.HtmlEncode(trainingTitle ?? "")}</p>" +
            $"<p>Puntuación: <b>{score}/{total}</b> ({pct}%).</p>";
        return Wrap.Replace("{BODY}", body);
    }
}

// ---- Alerta a autores/moderadores cuando alguien aprueba un curso ----
public static class CompletionAlert
{
    public static async Task SendAsync(CatalogDbContext catalog, IEmailSender email,
        Guid? tenantId, string? learnerName, string? trainingTitle, int score, int total)
    {
        if (tenantId is null) return;
        var recipients = await catalog.Users
            .Where(u => u.TenantId == tenantId && (u.Role == "Author" || u.Role == "Moderator" || u.Role == "Admin"))
            .Select(u => new { u.Email, u.Name }).ToListAsync();
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

            var users = await catalog.Users.Where(u => u.TenantId == t.Id)
                .Select(u => new { u.Id, u.Email, u.Name }).ToListAsync();

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
