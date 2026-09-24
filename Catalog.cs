using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TrainingPlatform.Catalog;

// Maps a client organization to the database where its data lives.
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public string Status { get; set; } = "active"; // active | disabled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Política de doble factor de ESTA compañía. La decide la compañía, no el usuario:
    //   off      = no se usa
    //   optional = quien quiera lo activa desde su perfil
    //   required = obligatorio; sin él no se entra
    public string TwoFactorPolicy { get; set; } = "optional";

    // Reglas de cumplimiento de la compañía (blob JSON, ver ComplianceConfig):
    // a quién más se copia, cómo se entrega el certificado y cadencia de avisos.
    public string ComplianceConfigJson { get; set; } = "{}";
}

// Enlace directo a un certificado (GET /c/{token}), con vencimiento. Vive en el
// catálogo porque el enlace es anónimo: hay que saber de qué compañía es ANTES de
// conectarse a su base. Del token solo se guarda el hash (SHA-256 en base64): quien
// lea la tabla no puede abrir ningún certificado.
public class CertificateLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CertificateId { get; set; }             // Certificate.Id en la base de la compañía
    public string Purpose { get; set; } = "learner";    // learner | officer | extra
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessAt { get; set; }
    public DateTime? RevokedAt { get; set; }            // no nulo = revocado a mano
}

// Reglas de cumplimiento por compañía, guardadas en Tenant.ComplianceConfigJson.
// Lectura tolerante (como NotificationConfig): si el blob falta, está corrupto o
// trae valores fuera de rango, se usa el valor por defecto de ese campo.
public class ComplianceConfig
{
    // Correos que reciben copia del certificado y de los resúmenes, además de los
    // oficiales de cumplimiento marcados.
    public List<string> ExtraEmails { get; set; } = new();
    public int DueSoonDays { get; set; } = 30;                  // ventana de "por vencer"
    public string DigestFrequency { get; set; } = "weekly";     // daily | weekly | monthly
    public string DigestDayOfWeek { get; set; } = "Monday";     // solo weekly
    public int DigestDayOfMonth { get; set; } = 1;              // solo monthly
    public int DigestHour { get; set; } = 8;                    // hora local mínima de envío
    public int ExpiredRepeatDays { get; set; } = 14;            // cada cuánto se repite un vencido
    public bool IncludeNotStarted { get; set; } = true;
    public string CertificateDelivery { get; set; } = "link";   // link | attachment
    public int LinkDays { get; set; } = 30;                     // vigencia del enlace del certificado

    public static ComplianceConfig Parse(string? json)
    {
        var cfg = new ComplianceConfig();
        System.Text.Json.Nodes.JsonObject? n;
        try { n = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!) as System.Text.Json.Nodes.JsonObject; }
        catch { return cfg; }
        if (n is null) return cfg;

        // Cada campo por separado: uno malo no tumba a los demás.
        if (n["extraEmails"] is System.Text.Json.Nodes.JsonArray arr)
            cfg.ExtraEmails = LimpiarCorreos(arr.Select(x => Texto(x)));
        else if (Texto(n["extraEmails"]) is string lista)   // también "a@x.com, b@y.com"
            cfg.ExtraEmails = LimpiarCorreos(lista.Split(',', ';'));

        cfg.DueSoonDays = Entero(n["dueSoonDays"], 1, 365) ?? cfg.DueSoonDays;
        var freq = Texto(n["digestFrequency"])?.Trim().ToLowerInvariant();
        if (freq is "daily" or "weekly" or "monthly") cfg.DigestFrequency = freq;
        if (Enum.TryParse<DayOfWeek>(Texto(n["digestDayOfWeek"])?.Trim(), true, out var dia) && Enum.IsDefined(dia))
            cfg.DigestDayOfWeek = dia.ToString();
        cfg.DigestDayOfMonth = Entero(n["digestDayOfMonth"], 1, 31) ?? cfg.DigestDayOfMonth;
        cfg.DigestHour = Entero(n["digestHour"], 0, 23) ?? cfg.DigestHour;
        cfg.ExpiredRepeatDays = Entero(n["expiredRepeatDays"], 1, 365) ?? cfg.ExpiredRepeatDays;
        try { if (n["includeNotStarted"] is not null) cfg.IncludeNotStarted = n["includeNotStarted"]!.GetValue<bool>(); } catch { }
        var entrega = Texto(n["certificateDelivery"])?.Trim().ToLowerInvariant();
        if (entrega is "link" or "attachment") cfg.CertificateDelivery = entrega;
        cfg.LinkDays = Entero(n["linkDays"], 1, 365) ?? cfg.LinkDays;
        return cfg;
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(new
    {
        extraEmails = ExtraEmails,
        dueSoonDays = DueSoonDays,
        digestFrequency = DigestFrequency,
        digestDayOfWeek = DigestDayOfWeek,
        digestDayOfMonth = DigestDayOfMonth,
        digestHour = DigestHour,
        expiredRepeatDays = ExpiredRepeatDays,
        includeNotStarted = IncludeNotStarted,
        certificateDelivery = CertificateDelivery,
        linkDays = LinkDays
    });

    private static string? Texto(System.Text.Json.Nodes.JsonNode? x)
    {
        try { return x?.GetValue<string>(); } catch { return null; }
    }

    // Acepta 30 y "30"; fuera de rango o ilegible = null (se queda el default).
    private static int? Entero(System.Text.Json.Nodes.JsonNode? x, int min, int max)
    {
        if (x is null) return null;
        int v;
        try { v = x.GetValue<int>(); }
        catch
        {
            if (!int.TryParse(Texto(x), out v)) return null;
        }
        return v >= min && v <= max ? v : null;
    }

    private static List<string> LimpiarCorreos(IEnumerable<string?> correos)
        => correos.Select(c => (c ?? "").Trim())
                  .Where(c => c.Length is > 3 and <= 254 && c.Contains('@') && !c.Contains(' '))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .Take(20)
                  .ToList();
}

// Un usuario puede pertenecer a varias compañías, con rol distinto en cada una.
// La compañía "principal" sigue viviendo en AppUser.TenantId (es la que se resuelve
// al entrar); esta tabla añade las demás.
public class UserCompany
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "Learner";   // Admin | Author | Moderator | Learner
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Identity + routing live centrally so we know the tenant before connecting to its DB.
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Learner"; // Admin | Author | Moderator | Learner
    public Guid? TenantId { get; set; }            // null = platform admin (no tenant)
    public bool MustChangePassword { get; set; }   // true tras un reseteo del admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Validación del correo. Mientras esté en null, la app pide confirmarlo con un
    // código antes de dejar entrar: el certificado se manda a esta dirección, así que
    // tiene que ser correcta y del propio empleado.
    public DateTime? EmailVerifiedAt { get; set; }

    // Verificación en dos pasos. "none" = solo contraseña.
    public string TwoFactorMode { get; set; } = "none";   // none | email | totp
    public string? TotpSecret { get; set; }               // base32, solo para el modo totp
    public DateTime? TwoFactorConfirmedAt { get; set; }   // null = configurado pero sin confirmar
}

// Reto de segundo factor: se crea al validar la contraseña y se consume con el código.
// Del código por correo solo se guarda el hash.
public class TwoFactorChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Mode { get; set; } = "email";      // email | totp
    public string Purpose { get; set; } = "login";   // login | enroll
    public string? CodeHash { get; set; }            // solo en modo email
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

// Token de recuperación de contraseña pedido desde el login.
// Se guarda solo el HASH del token: si alguien lee la tabla no puede usarlo.
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";      // SHA-256 del token enviado por correo
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }            // no nulo = ya se consumió
}

public class CatalogAuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public Guid? UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

// Quién pertenece a una compañía: los que la tienen como principal más los que
// llegan por UserCompany. Se usa en todos los sitios que antes filtraban por
// AppUser.TenantId, para que un usuario multicompañía cuente en las dos.
public static class CompanyUsers
{
    public record Miembro(Guid Id, string Email, string Name, string Role);

    public static async Task<List<Miembro>> OfAsync(CatalogDbContext catalog, Guid tenantId)
    {
        var principales = await catalog.Users.Where(u => u.TenantId == tenantId)
            .Select(u => new Miembro(u.Id, u.Email, u.Name, u.Role)).ToListAsync();

        var extras = await (from m in catalog.UserCompanies
                            where m.TenantId == tenantId
                            join u in catalog.Users on m.UserId equals u.Id
                            select new Miembro(u.Id, u.Email, u.Name, m.Role)).ToListAsync();

        return principales.Concat(extras).GroupBy(x => x.Id).Select(g => g.First()).ToList();
    }
}

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CatalogAuditLog> AuditLogs => Set<CatalogAuditLog>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<CertificateLink> CertificateLinks => Set<CertificateLink>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Nombres de tabla en singular.
        b.Entity<Tenant>().ToTable("Tenant");
        b.Entity<AppUser>().ToTable("User");
        b.Entity<CatalogAuditLog>().ToTable("AuditLog");

        b.Entity<PasswordResetToken>().ToTable("PasswordResetToken");

        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Tenant>().HasIndex(t => t.Name).IsUnique();
        b.Entity<PasswordResetToken>().HasIndex(t => t.TokenHash);
        b.Entity<PasswordResetToken>().HasIndex(t => t.UserId);
        b.Entity<TwoFactorChallenge>().ToTable("TwoFactorChallenge");
        b.Entity<TwoFactorChallenge>().HasIndex(c => c.UserId);
        b.Entity<UserCompany>().ToTable("UserCompany");
        b.Entity<UserCompany>().HasIndex(m => new { m.UserId, m.TenantId }).IsUnique();

        // Las filas que ya existían toman "{}" (config por defecto) al migrar.
        b.Entity<Tenant>().Property(t => t.ComplianceConfigJson).HasDefaultValue("{}");

        b.Entity<CertificateLink>().ToTable("CertificateLink");
        b.Entity<CertificateLink>().Property(l => l.TokenHash).HasMaxLength(64);
        b.Entity<CertificateLink>().Property(l => l.Purpose).HasMaxLength(16);
        b.Entity<CertificateLink>().HasIndex(l => l.TokenHash).IsUnique();
        b.Entity<CertificateLink>().HasIndex(l => new { l.TenantId, l.CertificateId });
    }
}

// Lets `dotnet ef migrations add ... --context CatalogDbContext` work at design time.
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();
        var opts = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(cfg.GetConnectionString("Catalog"))
            .Options;
        return new CatalogDbContext(opts);
    }
}
