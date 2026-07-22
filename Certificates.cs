using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.Catalog;
using TrainingPlatform.Multitenancy;
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
            if (!owner && !CanAuthor(tc.Role)) return Results.Forbid();
            if (!attempt.Passed) return Results.BadRequest("El intento no está aprobado; no hay certificado.");

            var cert = await CertificateService.EnsureIssuedAsync(db, attempt);
            if (cert is null) return Results.BadRequest("Este curso no emite certificado.");

            var cfg = CertificateConfig.Parse(cert.ConfigSnapshotJson);
            var issuer = await IssuerNameAsync(catalog, tc.TenantId);
            return Results.Ok(CertificateService.ToView(cert, cfg, issuer));
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
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static async Task<string> IssuerNameAsync(CatalogDbContext catalog, Guid? tenantId)
    {
        if (tenantId is null) return "";
        var name = await catalog.Tenants.Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefaultAsync();
        return name ?? "";
    }
}
