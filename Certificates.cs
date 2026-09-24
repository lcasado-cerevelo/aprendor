using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.Catalog;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.Notifications;
using TrainingPlatform.Certificates;
using TrainingPlatform.TenantData;

namespace TrainingPlatform;

// ============================================================================
//  Certificados de aprobación
//  --------------------------
//  La ESTRUCTURA del certificado (marco, sello, folio, disposición) es común a
//  todos los cursos de cumplimiento: eso vive en la plantilla del front
//  (certificate.html). Lo CONFIGURABLE POR CURSO son los campos de este record,
//  guardados en Training.CertificateConfigJson. Al aprobar se emite un
//  Certificate inmutable con estos datos "congelados".
// ============================================================================

// Config editable por el autor para un curso. Todos los campos tienen un valor
// por defecto sensato, así un curso sin configurar igual produce un certificado válido.
public class CertificateConfig
{
    // Si está desactivado, no se ofrece descarga de certificado para el curso.
    public bool Enabled { get; set; } = true;

    // Entidad emisora que encabeza el documento. Vacío => se usa el nombre del cliente (tenant).
    public string? IssuerName { get; set; }

    // Firma.
    public string? SignatoryName { get; set; }
    public string? SignatoryTitle { get; set; }

    // A quién se le manda el certificado además del propio learner: normalmente
    // quien lo archiva en el expediente del empleado. Uno o varios correos
    // separados por coma. No tiene que ser quien firma.
    public string? RecipientEmails { get; set; }

    // Firma manuscrita/escaneada embebida como data URL (base64), opcional.
    // Se muestra sobre la línea de firma. Se congela en el snapshot como el logo.
    public string? SignatureDataUrl { get; set; }

    // Leyenda / texto legal bajo el cuerpo (p. ej. la norma que cumple el adiestramiento).
    public string? Statement { get; set; }

    // Logo embebido como data URL (base64). Se congela en el snapshot para que el
    // documento sea autocontenido y no dependa de endpoints protegidos.
    public string? LogoDataUrl { get; set; }

    // Qué mostrar en el cuerpo.
    public bool ShowScore { get; set; } = true;
    public bool ShowValidity { get; set; } = true;

    // Color de acento del marco/sello. Mantiene un look consistente pero permite matiz por curso.
    public string AccentColor { get; set; } = "#1e3a8a";

    private static readonly JsonSerializerOptions J = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static CertificateConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new CertificateConfig();
        try { return JsonSerializer.Deserialize<CertificateConfig>(json, J) ?? new CertificateConfig(); }
        catch { return new CertificateConfig(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this, J);
}

public static class CertificateService
{
    // Emite (o devuelve el ya existente) certificado para un intento aprobado.
    // Idempotente: el índice único por AttemptId protege ante carreras.
    public static async Task<Certificate?> EnsureIssuedAsync(TenantDbContext db, Attempt attempt)
    {
        if (!attempt.Passed) return null;

        var existing = await db.Certificates.FirstOrDefaultAsync(c => c.AttemptId == attempt.Id);
        if (existing is not null) return existing;

        var version = await db.TrainingVersions.FindAsync(attempt.TrainingVersionId);
        if (version is null) return null;
        var training = await db.Trainings.FindAsync(version.TrainingId);
        if (training is null) return null;

        var cfg = CertificateConfig.Parse(training.CertificateConfigJson);
        if (!cfg.Enabled) return null; // el curso no emite certificados

        var total = await SetTotalAsync(db, attempt);
        int pct = total == 0 ? 100 : (int)Math.Round(attempt.Score * 100.0 / total);

        var completedAt = attempt.CompletedAt ?? DateTime.UtcNow;
        DateTime? expiresAt = training.RecurrenceMonths is int m and > 0
            ? completedAt.AddMonths(m)
            : null;

        var cert = new Certificate
        {
            Serial = MakeSerial(completedAt),
            AttemptId = attempt.Id,
            TrainingId = training.Id,
            TrainingVersionId = version.Id,
            UserId = attempt.UserId,
            LearnerName = string.IsNullOrWhiteSpace(attempt.LearnerName)
                ? (attempt.GuestName ?? "Participante") : attempt.LearnerName!,
            TrainingTitle = training.Title,
            ScorePercent = pct,
            PassPercent = version.PassPercent,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            ConfigSnapshotJson = cfg.ToJson()
        };
        db.Certificates.Add(cert);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otra petición lo emitió primero: reconciliamos con el que ganó.
            db.Entry(cert).State = EntityState.Detached;
            return await db.Certificates.FirstOrDefaultAsync(c => c.AttemptId == attempt.Id);
        }
        return cert;
    }

    // Folio legible y único. El GUID hace la unicidad; el índice único lo garantiza.
    private static string MakeSerial(DateTime when)
        => $"CERT-{when:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    // Réplica de la suma de puntos del set tomado (misma lógica que Phase2.SetTotalAsync).
    private static async Task<int> SetTotalAsync(TenantDbContext db, Attempt attempt)
    {
        var items = await db.TrainingItems
            .Where(i => i.TrainingVersionId == attempt.TrainingVersionId && i.Active)
            .Select(i => new { i.Points, i.StableKey }).ToListAsync();
        if (attempt.SetId is not null)
        {
            var excl = await db.TrainingSetExclusions.Where(e => e.SetId == attempt.SetId)
                .Select(e => e.StableKey).ToListAsync();
            if (excl.Count > 0) items = items.Where(i => !excl.Contains(i.StableKey)).ToList();
        }
        return items.Sum(i => i.Points);
    }

    // Proyección para el front (documento imprimible + verificación).
    public static object ToView(Certificate c, CertificateConfig cfg, string issuerFallback)
    {
        var now = DateTime.UtcNow;
        string validity = c.ExpiresAt is null ? "permanent"
            : (now >= c.ExpiresAt.Value ? "expired" : "valid");
        return new
        {
            serial = c.Serial,
            learnerName = c.LearnerName,
            trainingTitle = c.TrainingTitle,
            scorePercent = c.ScorePercent,
            passPercent = c.PassPercent,
            issuedAt = c.IssuedAt,
            expiresAt = c.ExpiresAt,
            validity, // permanent | valid | expired
            issuerName = string.IsNullOrWhiteSpace(cfg.IssuerName) ? issuerFallback : cfg.IssuerName,
            signatoryName = cfg.SignatoryName,
            signatoryTitle = cfg.SignatoryTitle,
            signatureDataUrl = cfg.SignatureDataUrl,
            statement = cfg.Statement,
            logoDataUrl = cfg.LogoDataUrl,
            showScore = cfg.ShowScore,
            showValidity = cfg.ShowValidity,
            accentColor = cfg.AccentColor
        };
    }
}

public static class CertificateEndpoints
{
    private static TenantDbContext? Db(IServiceProvider sp, ITenantContext tc)
        => tc.TenantId is null ? null : sp.GetRequiredService<TenantDbContext>();

    private static bool CanAuthor(string? role) => role is "Admin" or "Author" or "Moderator";

    // Quién puede reenviar, revocar y ver los enlaces de un certificado ajeno:
    // autores/moderadores/admin de la compañía y los oficiales de cumplimiento.
    private static async Task<bool> PuedeGestionarAsync(CatalogDbContext catalog, ITenantContext tc)
    {
        if (CanAuthor(tc.Role)) return true;
        if (tc.TenantId is not Guid tid || tc.UserId is not Guid uid) return false;
        return (await CertificateRecipients.OficialesDeCumplimientoAsync(catalog, tid)).Any(o => o.Id == uid);
    }

    // Admin de la compañía activa (no el admin de plataforma, que no tiene compañía).
    private static bool EsAdminEmpresa(ITenantContext tc) => tc.TenantId is not null && tc.Role == "Admin";

    // Manda el certificado al learner, a los oficiales de cumplimiento, a los correos
    // extra de la compañía y a quien esté configurado en el curso para archivarlo.
    // Según ComplianceConfig.certificateDelivery va por enlace con vencimiento (por
    // defecto) o con el PDF adjunto (comportamiento de antes). Nunca tumba el flujo de
    // completar un curso: si el correo falla, se traga el error.
    public static async Task MailAsync(CatalogDbContext catalog, IEmailSender email,
        Certificate cert, Guid? tenantId, string? appUrl)
    {
        try
        {
            var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
            if (!cfg.Enabled) return;

            var tenant = tenantId is null ? null : await catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            var comp = ComplianceConfig.Parse(tenant?.ComplianceConfigJson);
            var destinos = await CertificateRecipients.ResolverAsync(catalog, tenantId, cert, cfg, comp, "all");

            // La auditoría va a la base de la compañía. Aquí no hay TenantDbContext de la
            // petición a mano (la firma de MailAsync no cambia), así que se abre uno.
            TenantDbContext? auditoria = null;
            if (!string.IsNullOrWhiteSpace(tenant?.ConnectionString))
                auditoria = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
                    .UseSqlServer(tenant!.ConnectionString).Options);
            try
            {
                await EnviarAsync(catalog, email, auditoria, tenant, cert, cfg, comp, destinos,
                    porEnlace: comp.CertificateDelivery == "link", appUrl, "certificate-sent", actor: null);
            }
            finally
            {
                if (auditoria is not null) await auditoria.DisposeAsync();
            }
        }
        catch { /* el correo nunca rompe la aprobación */ }
    }

    // Envío común (primer envío y reenvíos). Por enlace crea un token nuevo POR
    // destinatario (cada uno con su propósito y su contador de accesos); si no se puede
    // armar un enlace absoluto (sin App:BaseUrl ni petición en curso) cae al adjunto.
    // Devuelve cuántos correos salieron.
    private static async Task<int> EnviarAsync(CatalogDbContext catalog, IEmailSender email, TenantDbContext? auditoria,
        Tenant? tenant, Certificate cert, CertificateConfig cfg, ComplianceConfig comp,
        List<CertificateRecipients.Destinatario> destinos, bool porEnlace, string? appUrl, string accion, Guid? actor)
    {
        if (destinos.Count == 0) return 0;

        var baseUrl = CertificateLinks.BaseUrl(appUrl);
        var inicio = baseUrl is null ? appUrl : baseUrl + "/";
        var enlace = porEnlace && tenant is not null && baseUrl is not null;

        EmailAttachment[]? adjunto = null;
        if (!enlace)
        {
            var emisor = string.IsNullOrWhiteSpace(cfg.IssuerName) ? (tenant?.Name ?? "") : cfg.IssuerName!;
            try
            {
                adjunto = new[] { new EmailAttachment(NombreArchivo(cert), RenderPdf(cert, cfg, emisor), "application/pdf") };
            }
            catch { return 0; }
        }

        int enviados = 0;
        foreach (var d in destinos)
        {
            var paraArchivo = d.Purpose != "learner";
            var asunto = paraArchivo
                ? $"Certificado: {cert.LearnerName} aprobó {cert.TrainingTitle}"
                : $"Tu certificado: {cert.TrainingTitle}";

            CertificateLink? registro = null;
            string? url = null;
            bool ok;
            try
            {
                if (enlace)
                {
                    (registro, var token) = await CertificateLinks.CrearAsync(catalog, tenant!.Id, cert.Id, d.Purpose, comp.LinkDays);
                    url = CertificateLinks.Url(baseUrl!, token);
                }
                await email.SendAsync(d.Email, d.Name, asunto,
                    EmailTemplates.CertificateIssued(cert.LearnerName, cert.TrainingTitle, cert.Serial,
                        cert.IssuedAt, cert.ExpiresAt, paraArchivo, inicio, url, comp.LinkDays), adjunto);
                ok = true;
                enviados++;
            }
            catch
            {
                ok = false;
                // Un enlace que no llegó a nadie no debe quedar vivo ni contar en el expediente.
                if (registro is not null)
                {
                    try { registro.RevokedAt = DateTime.UtcNow; await catalog.SaveChangesAsync(); } catch { }
                }
            }

            // Nunca se audita el token, solo a quién y cómo.
            auditoria?.AuditLogs.Add(new TenantAuditLog
            {
                Action = accion,
                Detail = $"{cert.Serial} -> {d.Purpose} {d.Email} ({(enlace ? $"enlace {comp.LinkDays} días" : "adjunto")}{(ok ? "" : ", falló el envío")})",
                UserId = actor
            });
        }

        if (auditoria is not null)
        {
            try { await auditoria.SaveChangesAsync(); } catch { }
        }
        return enviados;
    }

    private static byte[] RenderPdf(Certificate cert, CertificateConfig cfg, string emisor)
        => CertificatePdf.Render(emisor, cert.LearnerName, cert.TrainingTitle, cert.Serial,
            cert.IssuedAt, cert.ExpiresAt, cert.ScorePercent, cert.PassPercent,
            cfg.ShowScore, cfg.ShowValidity, cfg.Statement,
            cfg.SignatoryName, cfg.SignatoryTitle, cfg.AccentColor);

    private static string NombreArchivo(Certificate cert)
        => $"Certificado-{Limpiar(cert.TrainingTitle)}-{Limpiar(cert.LearnerName)}-{cert.Serial}.pdf";

    private static string Limpiar(string? s)
    {
        var limpio = new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-').ToArray()).Trim();
        limpio = limpio.Replace(' ', '-');
        return limpio.Length > 40 ? limpio[..40].TrimEnd('-') : (limpio.Length == 0 ? "documento" : limpio);
    }

    public static void MapCertificates(this WebApplication app)
    {
        // ---- Config por curso: leer (autor) ----
        app.MapGet("/trainings/{id:guid}/certificate-config", async (Guid id, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();
            return Results.Ok(CertificateConfig.Parse(t.CertificateConfigJson));
        }).RequireAuthorization();

        // ---- Config por curso: guardar (autor) ----
        app.MapPut("/trainings/{id:guid}/certificate-config", async (Guid id, CertificateConfig req, ITenantContext tc, IServiceProvider sp) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var t = await db.Trainings.FindAsync(id);
            if (t is null) return Results.NotFound();

            // Normaliza: recorta strings y evita logos gigantes (límite defensivo ~1.5 MB base64).
            req.IssuerName = Trim(req.IssuerName);
            req.SignatoryName = Trim(req.SignatoryName);
            req.SignatoryTitle = Trim(req.SignatoryTitle);
            req.Statement = Trim(req.Statement);
            req.RecipientEmails = Trim(req.RecipientEmails);
            if (!string.IsNullOrEmpty(req.LogoDataUrl))
            {
                if (!req.LogoDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("El logo debe ser una imagen (data URL).");
                if (req.LogoDataUrl.Length > 1_500_000)
                    return Results.BadRequest("El logo es demasiado grande (máx. ~1 MB).");
            }
            if (!string.IsNullOrEmpty(req.SignatureDataUrl))
            {
                if (!req.SignatureDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("La firma debe ser una imagen (data URL).");
                if (req.SignatureDataUrl.Length > 1_500_000)
                    return Results.BadRequest("La firma es demasiado grande (máx. ~1 MB).");
            }
            if (string.IsNullOrWhiteSpace(req.AccentColor)) req.AccentColor = "#1e3a8a";

            t.CertificateConfigJson = req.ToJson();
            await db.SaveChangesAsync();
            return Results.Ok(req);
        }).RequireAuthorization();

        // ---- Certificado de un intento: lo ve su dueño o un autor/moderador ----
        app.MapGet("/attempts/{attemptId:guid}/certificate", async (Guid attemptId, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var attempt = await db.Attempts.FindAsync(attemptId);
            if (attempt is null) return Results.NotFound();
            bool owner = attempt.UserId != null && attempt.UserId == tc.UserId;
            if (!owner && !await PuedeGestionarAsync(catalog, tc)) return Results.Forbid();
            if (!attempt.Passed) return Results.BadRequest("El intento no está aprobado; no hay certificado.");

            var cert = await CertificateService.EnsureIssuedAsync(db, attempt);
            if (cert is null) return Results.BadRequest("Este curso no emite certificado.");

            var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
            var issuer = await IssuerNameAsync(catalog, tc.TenantId);
            return Results.Ok(CertificateService.ToView(cert, cfg, issuer));
        }).RequireAuthorization();

        // ---- El certificado en PDF: lo baja su dueño, un autor, un moderador o un oficial de cumplimiento ----
        app.MapGet("/certificates/{serial}/pdf", async (string serial, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Serial == serial);
            if (cert is null) return Results.NotFound();
            if (cert.UserId != tc.UserId && !await PuedeGestionarAsync(catalog, tc)) return Results.Forbid();

            var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
            if (!cfg.Enabled) return Results.BadRequest("Este curso no emite certificado.");
            var emisor = string.IsNullOrWhiteSpace(cfg.IssuerName)
                ? await IssuerNameAsync(catalog, tc.TenantId) : cfg.IssuerName!;

            var pdf = CertificatePdf.Render(emisor, cert.LearnerName, cert.TrainingTitle, cert.Serial,
                cert.IssuedAt, cert.ExpiresAt, cert.ScorePercent, cert.PassPercent,
                cfg.ShowScore, cfg.ShowValidity, cfg.Statement,
                cfg.SignatoryName, cfg.SignatoryTitle, cfg.AccentColor);

            return Results.File(pdf, "application/pdf", $"Certificado-{cert.Serial}.pdf");
        }).RequireAuthorization();

        // ---- Verificación por folio (usuario autenticado del cliente) ----
        // Devuelve el estado de un certificado a partir de su folio. La búsqueda es
        // dentro del cliente (tenant) actual, coherente con el aislamiento por BD.
        app.MapGet("/certificates/{serial}", async (string serial, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Serial == serial);
            if (cert is null) return Results.NotFound(new { found = false });
            var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
            var issuer = await IssuerNameAsync(catalog, tc.TenantId);
            return Results.Ok(CertificateService.ToView(cert, cfg, issuer));
        }).RequireAuthorization();

        // ---- Enlace directo al certificado (anónimo, con vencimiento) ----
        // Lo abre quien recibió el correo, sin entrar a la plataforma. El token viaja
        // solo en la URL; en la base está su hash. NUNCA se escribe el token en logs
        // ni en la auditoría. Si el enlace venció, fue revocado o no existe, se responde
        // una página mínima sin ningún dato personal.
        app.MapGet("/c/{token}", async (string token, HttpContext http, CatalogDbContext catalog,
            IConfiguration config, ILoggerFactory logs) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers["Referrer-Policy"] = "no-referrer";   // que el token no se filtre por Referer
            http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            var inicio = (CertificateLinks.BaseUrl(config["App:BaseUrl"]) ?? "") + "/";

            if (!LimitePorIp.Permitir(http.Connection.RemoteIpAddress?.ToString()))
                return Results.Content(CertificateLinks.PaginaNoDisponible(inicio, demasiadas: true),
                    "text/html; charset=utf-8", statusCode: StatusCodes.Status429TooManyRequests);

            var vencido = Results.Content(CertificateLinks.PaginaNoDisponible(inicio),
                "text/html; charset=utf-8", statusCode: StatusCodes.Status410Gone);
            if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return vencido;

            var ahora = DateTime.UtcNow;
            var hash = CertificateLinks.Hash(token);
            var link = await catalog.CertificateLinks.AsNoTracking().FirstOrDefaultAsync(l => l.TokenHash == hash);
            if (link is null || link.RevokedAt is not null || link.ExpiresAt <= ahora) return vencido;

            try
            {
                var tenant = await catalog.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == link.TenantId && t.Status == "active");
                if (tenant is null || string.IsNullOrWhiteSpace(tenant.ConnectionString)) return vencido;

                var opts = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(tenant.ConnectionString).Options;
                await using var db = new TenantDbContext(opts);
                var cert = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == link.CertificateId);
                if (cert is null) return vencido;

                // Se regenera desde el snapshot congelado al emitir: es el mismo documento siempre.
                var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
                if (!cfg.Enabled) return vencido;
                var emisor = string.IsNullOrWhiteSpace(cfg.IssuerName) ? tenant.Name : cfg.IssuerName!;
                var pdf = RenderPdf(cert, cfg, emisor);

                // Contador atómico (dos aperturas a la vez no se pisan).
                await catalog.CertificateLinks.Where(l => l.Id == link.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(l => l.AccessCount, l => l.AccessCount + 1)
                        .SetProperty(l => l.LastAccessAt, ahora));

                var disposicion = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
                disposicion.SetHttpFileName(NombreArchivo(cert));
                http.Response.Headers.ContentDisposition = disposicion.ToString();
                return Results.File(pdf, "application/pdf");
            }
            catch (Exception ex)
            {
                // Se identifica el enlace por su Id, nunca por el token.
                logs.CreateLogger("CertificateLink").LogWarning(ex, "No se pudo servir el certificado del enlace {LinkId}", link.Id);
                return Results.Content(CertificateLinks.PaginaNoDisponible(inicio, error: true),
                    "text/html; charset=utf-8", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        // ---- Reenviar el certificado con un enlace nuevo ----
        // El dueño se lo reenvía a sí mismo. Autor/admin/oficial pueden además mandarlo
        // a las copias de cumplimiento ("copies") o a todos ("all"). Cuerpo opcional:
        // { "to": "learner" | "copies" | "all" }  (por defecto "learner").
        app.MapPost("/certificates/{serial}/resend", async (string serial, ResendCertificateRequest? req,
            ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog, IEmailSender email, IConfiguration config) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Serial == serial);
            if (cert is null) return Results.NotFound();
            bool owner = cert.UserId is not null && cert.UserId == tc.UserId;
            bool gestor = await PuedeGestionarAsync(catalog, tc);
            if (!owner && !gestor) return Results.Forbid();

            var alcance = (req?.To ?? "learner").Trim().ToLowerInvariant();
            if (alcance is not ("learner" or "copies" or "all"))
                return Results.BadRequest("Destino inválido: learner | copies | all.");
            if (alcance != "learner" && !gestor) return Results.Forbid();

            var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
            if (!cfg.Enabled) return Results.BadRequest("Este curso no emite certificado.");

            // Freno anti-bombardeo: un reenvío por certificado por minuto.
            var ahora = DateTime.UtcNow;
            if (await catalog.CertificateLinks.AnyAsync(l => l.TenantId == tc.TenantId && l.CertificateId == cert.Id
                                                           && l.CreatedAt > ahora.AddMinutes(-1)))
                return Results.Json("Se acaba de enviar; espera un minuto antes de reenviarlo.",
                    statusCode: StatusCodes.Status429TooManyRequests);

            var tenant = await catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tc.TenantId);
            var comp = ComplianceConfig.Parse(tenant?.ComplianceConfigJson);
            var destinos = await CertificateRecipients.ResolverAsync(catalog, tc.TenantId, cert, cfg, comp, alcance);
            if (destinos.Count == 0)
                return Results.BadRequest(alcance == "learner"
                    ? "El empleado no tiene un correo registrado."
                    : "No hay destinatarios configurados para las copias de cumplimiento.");

            // El reenvío siempre genera un enlace nuevo (el adjunto solo si no hay forma de armar la URL).
            var appUrl = config["App:BaseUrl"];
            var porEnlace = tenant is not null && CertificateLinks.BaseUrl(appUrl) is not null;
            var enviados = await EnviarAsync(catalog, email, db, tenant, cert, cfg, comp, destinos,
                porEnlace: true, appUrl, "certificate-resent", tc.UserId);

            return Results.Ok(new
            {
                serial = cert.Serial,
                sent = enviados,
                delivery = porEnlace ? "link" : "attachment",
                linkDays = porEnlace ? comp.LinkDays : (int?)null,
                recipients = destinos.Select(d => new { purpose = d.Purpose, email = d.Email })
            });
        }).RequireAuthorization();

        // ---- Revocar enlaces vivos de un certificado ----
        // Cuerpo opcional { "linkId": "..." } para revocar uno solo; sin él, todos los vivos.
        // El dueño solo puede revocar sus propios enlaces (propósito "learner"); los de
        // las copias de cumplimiento los revoca autor/admin/oficial.
        app.MapPost("/certificates/{serial}/revoke", async (string serial, RevokeCertificateLinksRequest? req,
            ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Serial == serial);
            if (cert is null) return Results.NotFound();
            bool owner = cert.UserId is not null && cert.UserId == tc.UserId;
            bool gestor = await PuedeGestionarAsync(catalog, tc);
            if (!owner && !gestor) return Results.Forbid();

            var ahora = DateTime.UtcNow;
            var q = catalog.CertificateLinks.Where(l => l.TenantId == tc.TenantId && l.CertificateId == cert.Id
                                                     && l.RevokedAt == null && l.ExpiresAt > ahora);
            if (!gestor) q = q.Where(l => l.Purpose == "learner");
            if (req?.LinkId is Guid linkId) q = q.Where(l => l.Id == linkId);

            var vivos = await q.ToListAsync();
            foreach (var l in vivos) l.RevokedAt = ahora;
            await catalog.SaveChangesAsync();

            db.AuditLogs.Add(new TenantAuditLog
            {
                Action = "certificate-links-revoked",
                Detail = $"{cert.Serial}: {vivos.Count} enlace(s)" +
                         (vivos.Count > 0 ? $" ({string.Join(", ", vivos.Select(v => v.Purpose).Distinct())})" : ""),
                UserId = tc.UserId
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { serial = cert.Serial, revoked = vivos.Count });
        }).RequireAuthorization();

        // ---- Enlaces de un certificado y cuántas veces se abrieron ----
        app.MapGet("/certificates/{serial}/links", async (string serial, ITenantContext tc, IServiceProvider sp, CatalogDbContext catalog) =>
        {
            var db = Db(sp, tc); if (db is null) return Results.BadRequest("No tenant context.");
            var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Serial == serial);
            if (cert is null) return Results.NotFound();
            bool owner = cert.UserId is not null && cert.UserId == tc.UserId;
            if (!owner && !await PuedeGestionarAsync(catalog, tc)) return Results.Forbid();

            var ahora = DateTime.UtcNow;
            var links = await catalog.CertificateLinks.AsNoTracking()
                .Where(l => l.TenantId == tc.TenantId && l.CertificateId == cert.Id)
                .OrderByDescending(l => l.CreatedAt).ToListAsync();

            return Results.Ok(new
            {
                serial = cert.Serial,
                totalAccesses = links.Sum(l => l.AccessCount),
                lastAccessAt = links.Max(l => l.LastAccessAt),
                activeLinks = links.Count(l => l.RevokedAt == null && l.ExpiresAt > ahora),
                links = links.Select(l => new
                {
                    id = l.Id,
                    purpose = l.Purpose,
                    createdAt = l.CreatedAt,
                    expiresAt = l.ExpiresAt,
                    accessCount = l.AccessCount,
                    lastAccessAt = l.LastAccessAt,
                    revokedAt = l.RevokedAt,
                    status = l.RevokedAt != null ? "revoked" : (l.ExpiresAt <= ahora ? "expired" : "active")
                })
            });
        }).RequireAuthorization();

        // ---- Reglas de cumplimiento de la compañía (Tenant.ComplianceConfigJson) ----
        // Leer: Admin de la compañía u oficial de cumplimiento. Cambiar: solo el Admin.
        app.MapGet("/company/compliance", async (ITenantContext tc, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!EsAdminEmpresa(tc) && !await ComplianceAccess.EsOficialAsync(catalog, tc)) return Results.Forbid();
            var t = await catalog.Tenants.FindAsync(tc.TenantId);
            if (t is null) return Results.NotFound();
            return Results.Ok(ComplianceConfig.Parse(t.ComplianceConfigJson));
        }).RequireAuthorization();

        // Acepta el objeto completo o solo los campos a cambiar: se mezclan con lo guardado
        // y se vuelve a leer con la lectura tolerante (valores inválidos = por defecto).
        app.MapPut("/company/compliance", async (HttpRequest http, ITenantContext tc, CatalogDbContext catalog) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!EsAdminEmpresa(tc)) return Results.Forbid();
            var t = await catalog.Tenants.FindAsync(tc.TenantId);
            if (t is null) return Results.NotFound();

            JsonObject? cambios;
            try
            {
                using var lector = new StreamReader(http.Body);
                cambios = JsonNode.Parse(await lector.ReadToEndAsync()) as JsonObject;
            }
            catch { cambios = null; }
            if (cambios is null) return Results.BadRequest("Se esperaba un objeto JSON.");

            JsonObject actual;
            try { actual = JsonNode.Parse(ComplianceConfig.Parse(t.ComplianceConfigJson).ToJson())!.AsObject(); }
            catch { actual = new JsonObject(); }
            foreach (var (clave, valor) in cambios.ToList())
            {
                if (string.IsNullOrEmpty(clave)) continue;
                var camel = char.ToLowerInvariant(clave[0]) + clave[1..];   // ExtraEmails -> extraEmails
                actual[camel] = valor?.DeepClone();
            }

            var cfg = ComplianceConfig.Parse(actual.ToJsonString());
            t.ComplianceConfigJson = cfg.ToJson();
            catalog.AuditLogs.Add(new CatalogAuditLog { Action = "compliance-config", Detail = t.Name, UserId = tc.UserId });
            await catalog.SaveChangesAsync();
            return Results.Ok(cfg);
        }).RequireAuthorization();
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static async Task<string> IssuerNameAsync(CatalogDbContext catalog, Guid? tenantId)
    {
        if (tenantId is null) return "";
        var name = await catalog.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefaultAsync();
        return name ?? "";
    }
}

// ============================================================================
//  Destinatarios del certificado
// ============================================================================
public static class CertificateRecipients
{
    // Purpose: learner (el propio empleado) | officer (oficial de cumplimiento) |
    // extra (ComplianceConfig.extraEmails y CertificateConfig.RecipientEmails del curso).
    public record Destinatario(string Email, string Name, string Purpose);

    // Quién recibe el certificado. `alcance`: learner = solo el empleado; copies = solo
    // las copias de cumplimiento; all = todos. Sin duplicados (un correo recibe un solo
    // mensaje, con el propósito de mayor prioridad: learner > officer > extra).
    public static async Task<List<Destinatario>> ResolverAsync(CatalogDbContext catalog, Guid? tenantId,
        Certificate cert, CertificateConfig cfg, ComplianceConfig comp, string alcance)
    {
        var lista = new List<Destinatario>();
        void Sumar(string? correo, string nombre, string proposito)
        {
            var c = (correo ?? "").Trim();
            if (c.Length == 0 || !c.Contains('@')) return;
            if (lista.Any(x => string.Equals(x.Email, c, StringComparison.OrdinalIgnoreCase))) return;
            lista.Add(new Destinatario(c, nombre, proposito));
        }

        // 1) el propio learner (un invitado sin cuenta no tiene a dónde recibirlo)
        if (alcance is "learner" or "all" && cert.UserId is Guid uid)
        {
            var learner = await catalog.Users.Where(u => u.Id == uid)
                .Select(u => new { u.Email, u.Name }).FirstOrDefaultAsync();
            if (learner is not null) Sumar(learner.Email, learner.Name, "learner");
        }

        if (alcance is "copies" or "all")
        {
            // 2) oficiales de cumplimiento de la compañía
            if (tenantId is Guid tid)
                foreach (var o in await OficialesDeCumplimientoAsync(catalog, tid))
                    Sumar(o.Email, o.Name, "officer");

            // 3) correos extra de la compañía
            foreach (var c in comp.ExtraEmails) Sumar(c, "", "extra");

            // 4) quien archiva el certificado según el curso (separados por coma)
            foreach (var c in (cfg.RecipientEmails ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Sumar(c, "", "extra");
        }
        return lista;
    }

    // Oficiales de cumplimiento de la compañía (marca UserCompany.IsComplianceOfficer):
    // reciben la copia del certificado con el nombre del empleado y pueden gestionar
    // sus enlaces. Los correos de ComplianceConfig.extraEmails se suman aparte.
    public static Task<List<CompanyUsers.Miembro>> OficialesDeCumplimientoAsync(CatalogDbContext catalog, Guid tenantId)
        => ComplianceOfficers.OfAsync(catalog, tenantId);
}

// ============================================================================
//  Enlaces directos al certificado (/c/{token})
// ============================================================================
public static class CertificateLinks
{
    // Origen de la petición en curso (https://host[/base]) para armar enlaces absolutos
    // cuando App:BaseUrl está vacío (es el caso normal: la URL sale de la petición).
    // Lo fija un middleware en Program.cs; al ser AsyncLocal, cada petición ve el suyo
    // aunque corran varias a la vez, y fuera de una petición queda en null.
    private static readonly AsyncLocal<string?> _origen = new();
    public static string? OrigenPeticion { get => _origen.Value; set => _origen.Value = value; }

    // App:BaseUrl si está configurado; si no, el origen de la petición; si tampoco, null.
    public static string? BaseUrl(string? configurada)
    {
        var b = string.IsNullOrWhiteSpace(configurada) ? OrigenPeticion : configurada;
        return string.IsNullOrWhiteSpace(b) ? null : b!.Trim().TrimEnd('/');
    }

    public static string Url(string baseUrl, string token) => $"{baseUrl}/c/{token}";

    // 32 bytes aleatorios en base64url (lo que va en la URL) y su SHA-256 en base64 (lo que se guarda).
    public static (string token, string hash) NuevoToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }

    public static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static async Task<(CertificateLink link, string token)> CrearAsync(CatalogDbContext catalog,
        Guid tenantId, Guid certificateId, string purpose, int dias)
    {
        var (token, hash) = NuevoToken();
        var ahora = DateTime.UtcNow;
        var link = new CertificateLink
        {
            TenantId = tenantId,
            CertificateId = certificateId,
            Purpose = purpose,
            TokenHash = hash,
            CreatedAt = ahora,
            ExpiresAt = ahora.AddDays(Math.Clamp(dias, 1, 365))
        };
        catalog.CertificateLinks.Add(link);
        await catalog.SaveChangesAsync();
        return (link, token);
    }

    // Página mínima para un enlace que ya no sirve. Sin datos personales: ni nombre,
    // ni curso, ni folio; solo el aviso y el botón para entrar a la plataforma.
    public static string PaginaNoDisponible(string inicio, bool demasiadas = false, bool error = false)
    {
        var (titulo, texto) = demasiadas
            ? ("Demasiados intentos", "Espera unos minutos y vuelve a abrir el enlace.")
            : error
                ? ("No pudimos abrir el certificado", "Inténtalo de nuevo más tarde o búscalo en tu expediente dentro de Aprendor.")
                : ("Este enlace venció", "Por seguridad, los enlaces a certificados duran un tiempo limitado. " +
                                          "Entra a Aprendor para ver el certificado en tu expediente.");
        var href = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(inicio) ? "/" : inicio);
        return "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">" +
               "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
               "<meta name=\"robots\" content=\"noindex,nofollow\">" +
               $"<title>{titulo} · Aprendor</title></head>" +
               "<body style=\"margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;" +
               "background:#0b1220;font:400 16px/1.6 Segoe UI,Arial,sans-serif;color:#0f172a;padding:16px;box-sizing:border-box;\">" +
               "<div style=\"background:#fff;border-radius:12px;max-width:420px;width:100%;padding:32px 28px;text-align:center;\">" +
               $"<h1 style=\"font-size:22px;margin:0 0 10px;\">{titulo}</h1>" +
               $"<p style=\"margin:0 0 24px;color:#475569;\">{texto}</p>" +
               $"<a href=\"{href}\" style=\"display:inline-block;background:#4f46e5;color:#fff;text-decoration:none;" +
               "font-weight:700;padding:12px 24px;border-radius:12px;\">Entrar a Aprendor</a>" +
               "</div></body></html>";
    }
}

// Límite de peticiones por IP para /c/{token}: ventana fija en memoria (se reinicia
// con la app). Con tokens de 256 bits adivinar es inútil; esto frena el martilleo.
internal static class LimitePorIp
{
    private const int Maximo = 60;
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, (DateTime inicio, int cuenta)> _ips = new();

    public static bool Permitir(string? ip)
    {
        var ahora = DateTime.UtcNow;
        // Limpieza perezosa para que el diccionario no crezca sin fin.
        if (_ips.Count > 10_000)
            foreach (var kv in _ips)
                if (ahora - kv.Value.inicio > Ventana) _ips.TryRemove(kv.Key, out _);

        var r = _ips.AddOrUpdate(string.IsNullOrWhiteSpace(ip) ? "?" : ip!,
            _ => (ahora, 1),
            (_, v) => ahora - v.inicio > Ventana ? (ahora, 1) : (v.inicio, v.cuenta + 1));
        return r.cuenta <= Maximo;
    }
}

public record ResendCertificateRequest(string? To);
public record RevokeCertificateLinksRequest(Guid? LinkId);
