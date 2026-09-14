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
