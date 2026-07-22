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
}

public class CatalogAuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public Guid? UserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CatalogAuditLog> AuditLogs => Set<CatalogAuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Nombres de tabla en singular.
        b.Entity<Tenant>().ToTable("Tenant");
        b.Entity<AppUser>().ToTable("User");
        b.Entity<CatalogAuditLog>().ToTable("AuditLog");

        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Tenant>().HasIndex(t => t.Name).IsUnique();
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
