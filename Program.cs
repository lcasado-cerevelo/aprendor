using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrainingPlatform;
using TrainingPlatform.Auth;
using TrainingPlatform.Catalog;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.Notifications;
using TrainingPlatform.TenantData;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Catalog context: fixed connection from config.
builder.Services.AddDbContext<CatalogDbContext>(o =>
    o.UseSqlServer(cfg.GetConnectionString("Catalog")));

// Tenant context: connection is resolved per request from ITenantContext.
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<TenantDbContext>((sp, o) =>
{
    var tc = sp.GetRequiredService<ITenantContext>();
    if (!string.IsNullOrWhiteSpace(tc.ConnectionString))
        o.UseSqlServer(tc.ConnectionString);
});

builder.Services.AddSingleton<JwtTokenService>();

// Correo + resumen semanal (opt-in vía Email:DigestEnabled).
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<WeeklyDigestService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "sub" / "role" / "tenant_id" as-is
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                string? t = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(t)) ctx.Token = t;
                return Task.CompletedTask;
            }
        };
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidAudience = cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization(o =>
    o.AddPolicy("Admin", p => p.RequireClaim("role", "Admin")));

var app = builder.Build();

// ---- CLI mode: apply migrations to the catalog + every tenant database ----
if (args.Length > 0 && args[0].Equals("migrate", StringComparison.OrdinalIgnoreCase))
{
    await MigrationRunner.RunAsync(app.Services);
    return;
}

// ---- On startup: migrate catalog and ensure a platform admin exists ----
using (var scope = app.Services.CreateScope())
{
    var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    catalog.Database.Migrate();
    await Bootstrap.SeedAdminAsync(catalog, cfg);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// ---------- Auth ----------
app.MapPost("/auth/login", async (LoginRequest req, CatalogDbContext catalog, JwtTokenService jwt) =>
{
    var user = await catalog.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
    if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    return Results.Ok(new
    {
        token = jwt.Create(user),
        user = new { user.Id, user.Email, user.Name, user.Role, user.TenantId, user.MustChangePassword }
    });
});

// Cambiar la propia contraseña (también limpia el flag de cambio obligatorio).
app.MapPost("/me/password", async (ChangePasswordRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    if (tc.UserId is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
        return Results.BadRequest("La nueva contraseña debe tener al menos 6 caracteres.");
    var user = await catalog.Users.FindAsync(tc.UserId.Value);
    if (user is null) return Results.NotFound();
    if (!PasswordHasher.Verify(req.CurrentPassword, user.PasswordHash))
        return Results.BadRequest("La contraseña actual no es correcta.");
    user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
    user.MustChangePassword = false;
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/me", (ITenantContext tc) =>
    Results.Ok(new { tc.UserId, tc.TenantId, tc.Role })).RequireAuthorization();

// ---------- Admin: tenants & users (platform admin only) ----------
app.MapPost("/admin/tenants", async (CreateTenantRequest req, CatalogDbContext catalog) =>
{
    if (await catalog.Tenants.AnyAsync(t => t.Name == req.Name))
        return Results.Conflict("A tenant with that name already exists.");

    var tenant = new Tenant { Name = req.Name, ConnectionString = req.ConnectionString };
    catalog.Tenants.Add(tenant);
    await catalog.SaveChangesAsync();

    // Create + migrate the new tenant's database right away.
    var opts = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(tenant.ConnectionString).Options;
    await using (var tdb = new TenantDbContext(opts))
        await tdb.Database.MigrateAsync();

    return Results.Ok(new { tenant.Id, tenant.Name });
}).RequireAuthorization("Admin");

app.MapGet("/admin/tenants", async (CatalogDbContext catalog) =>
    Results.Ok(await catalog.Tenants.OrderBy(t => t.Name)
        .Select(t => new { t.Id, t.Name, t.Status }).ToListAsync()))
    .RequireAuthorization("Admin");

// Dispara el resumen semanal de inmediato (para probar o forzar un envío).
app.MapPost("/admin/run-digest", async (IServiceProvider sp, IEmailSender email) =>
{
    var sent = await DigestRunner.RunAsync(sp, email);
    return Results.Ok(new { ran = true, sent });
}).RequireAuthorization("Admin");

app.MapPost("/admin/users", async (CreateUserRequest req, CatalogDbContext catalog, IEmailSender email, IConfiguration cfg) =>
{
    if (await catalog.Users.AnyAsync(u => u.Email == req.Email))
        return Results.Conflict("Email already exists.");

    var user = new AppUser
    {
        Email = req.Email,
        Name = req.Name,
        Role = req.Role,
        TenantId = req.TenantId,
        MustChangePassword = true,   // primer login: obliga a cambiarla
        PasswordHash = PasswordHasher.Hash(req.Password)
    };
    catalog.Users.Add(user);
    await catalog.SaveChangesAsync();

    bool invited = false;
    if (req.SendInvite == true)
    {
        try
        {
            await email.SendAsync(user.Email, user.Name, "Invitación a Aprendor",
                EmailTemplates.Invitation(user.Name, user.Email, req.Password, cfg["App:BaseUrl"]));
            invited = !string.IsNullOrWhiteSpace(cfg["Email:Host"]); // false si el correo no está configurado
        }
        catch { invited = false; }
    }
    return Results.Ok(new { user.Id, user.Email, user.Role, user.TenantId, invited });
}).RequireAuthorization("Admin");

// Listar usuarios (opcionalmente filtrados por cliente).
app.MapGet("/admin/users", async (Guid? tenantId, CatalogDbContext catalog) =>
{
    var q = catalog.Users.AsQueryable();
    if (tenantId is not null) q = q.Where(u => u.TenantId == tenantId);
    var users = await q.OrderBy(u => u.Name)
        .Select(u => new { u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword })
        .ToListAsync();
    return Results.Ok(users);
}).RequireAuthorization("Admin");

// Cambiar el rol de un usuario.
app.MapPost("/admin/users/{id:guid}/role", async (Guid id, RoleRequest req, CatalogDbContext catalog) =>
{
    var allowed = new[] { "Admin", "Author", "Moderator", "Learner" };
    if (!allowed.Contains(req.Role)) return Results.BadRequest("Rol inválido.");
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    user.Role = req.Role;
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("Admin");

// Resetear la contraseña de un usuario: el usuario deberá cambiarla al próximo login.
app.MapPost("/admin/users/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req, CatalogDbContext catalog) =>
{
    if (string.IsNullOrWhiteSpace(req.TempPassword) || req.TempPassword.Length < 6)
        return Results.BadRequest("La contraseña temporal debe tener al menos 6 caracteres.");
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    user.PasswordHash = PasswordHasher.Hash(req.TempPassword);
    user.MustChangePassword = true;
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("Admin");

// Eliminar un usuario (no puedes eliminarte a ti mismo).
app.MapDelete("/admin/users/{id:guid}", async (Guid id, ITenantContext tc, CatalogDbContext catalog) =>
{
    if (tc.UserId == id) return Results.BadRequest("No puedes eliminar tu propio usuario.");
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    catalog.Users.Remove(user);
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("Admin");

// Activar / desactivar un cliente.
app.MapPost("/admin/tenants/{id:guid}/status", async (Guid id, StatusRequest req, CatalogDbContext catalog) =>
{
    var allowed = new[] { "active", "inactive" };
    if (!allowed.Contains(req.Status)) return Results.BadRequest("Estado inválido.");
    var tenant = await catalog.Tenants.FindAsync(id);
    if (tenant is null) return Results.NotFound();
    tenant.Status = req.Status;
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("Admin");

// ---------- Tenant-scoped content (proves multitenancy end to end) ----------
app.MapGet("/categories", (ITenantContext tc, IServiceProvider sp) =>
{
    if (tc.TenantId is null) return Results.BadRequest("No tenant context for this user.");
    var db = sp.GetRequiredService<TenantDbContext>();
    return Results.Ok(db.Categories.OrderBy(c => c.Name).ToList());
}).RequireAuthorization();

app.MapPost("/categories", async (CreateCategoryRequest req, ITenantContext tc, IServiceProvider sp) =>
{
    if (tc.TenantId is null) return Results.BadRequest("No tenant context for this user.");
    var db = sp.GetRequiredService<TenantDbContext>();
    var cat = new Category { Name = req.Name, ParentId = req.ParentId };
    db.Categories.Add(cat);
    await db.SaveChangesAsync();
    return Results.Ok(cat);
}).RequireAuthorization();

app.MapGet("/trainings", (ITenantContext tc, IServiceProvider sp) =>
{
    if (tc.TenantId is null) return Results.BadRequest("No tenant context for this user.");
    var db = sp.GetRequiredService<TenantDbContext>();
    return Results.Ok(db.Trainings.OrderByDescending(t => t.CreatedAt).ToList());
}).RequireAuthorization();

app.MapPost("/trainings", async (CreateTrainingRequest req, ITenantContext tc, IServiceProvider sp) =>
{
    if (tc.TenantId is null) return Results.BadRequest("No tenant context for this user.");
    var db = sp.GetRequiredService<TenantDbContext>();
    var t = new Training
    {
        Title = req.Title,
        Description = req.Description,
        CategoryId = req.CategoryId,
        CreatedByUserId = tc.UserId
    };
    db.Trainings.Add(t);
    await db.SaveChangesAsync();
    return Results.Ok(t);
}).RequireAuthorization();

app.MapPhase2();
app.MapCertificates();

app.Run();


// ===================== Support types =====================

// Applies pending migrations to the catalog and to every active tenant database.
static class MigrationRunner
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        Console.WriteLine("Migrating catalog database...");
        await catalog.Database.MigrateAsync();

        var tenants = await catalog.Tenants.Where(t => t.Status == "active").ToListAsync();
        foreach (var t in tenants)
        {
            Console.WriteLine($"Migrating tenant '{t.Name}'...");
            var opts = new DbContextOptionsBuilder<TenantDbContext>()
                .UseSqlServer(t.ConnectionString).Options;
            await using var tdb = new TenantDbContext(opts);
            await tdb.Database.MigrateAsync();
        }

        Console.WriteLine($"Done. Catalog + {tenants.Count} tenant database(s) migrated.");
    }
}

static class Bootstrap
{
    public static async Task SeedAdminAsync(CatalogDbContext catalog, IConfiguration cfg)
    {
        if (await catalog.Users.AnyAsync()) return;

        var email = cfg["Bootstrap:AdminEmail"] ?? "admin@local";
        var pass = cfg["Bootstrap:AdminPassword"] ?? "ChangeMe123!";
        catalog.Users.Add(new AppUser
        {
            Email = email,
            Name = "Platform Admin",
            Role = "Admin",
            TenantId = null,
            PasswordHash = PasswordHasher.Hash(pass)
        });
        await catalog.SaveChangesAsync();
        Console.WriteLine($"Seeded platform admin: {email}");
    }
}

// ---- Request DTOs ----
record LoginRequest(string Email, string Password);
record CreateTenantRequest(string Name, string ConnectionString);
record CreateUserRequest(string Email, string Name, string Password, string Role, Guid? TenantId, bool? SendInvite);
record CreateCategoryRequest(string Name, Guid? ParentId);
record CreateTrainingRequest(string Title, string? Description, Guid? CategoryId);
record ChangePasswordRequest(string CurrentPassword, string NewPassword);
record RoleRequest(string Role);
record ResetPasswordRequest(string TempPassword);
record StatusRequest(string Status);
