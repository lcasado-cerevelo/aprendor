using System.Security.Cryptography;
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
app.MapPost("/auth/login", async (LoginRequest req, CatalogDbContext catalog, JwtTokenService jwt,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
    if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    // Con doble factor activo, la contraseña sola no entrega el token: abre un reto.
    if (user.TwoFactorMode is "email" or "totp" && user.TwoFactorConfirmedAt is not null)
    {
        var reto = await DosFactores.AbrirRetoAsync(catalog, user, "login", email, logs, cfg);
        return Results.Ok(new { requires2fa = true, mode = user.TwoFactorMode, challengeId = reto.Id });
    }

    return Results.Ok(new
    {
        token = jwt.Create(user),
        user = new { user.Id, user.Email, user.Name, user.Role, user.TenantId, user.MustChangePassword, user.TwoFactorMode }
    });
});

// Segundo paso del login: canjear el código por el token.
app.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest req, CatalogDbContext catalog, JwtTokenService jwt) =>
{
    var (ok, user, error) = await DosFactores.ConsumirAsync(catalog, req.ChallengeId, req.Code, "login");
    if (!ok || user is null) return Results.BadRequest(error);

    return Results.Ok(new
    {
        token = jwt.Create(user),
        user = new { user.Id, user.Email, user.Name, user.Role, user.TenantId, user.MustChangePassword, user.TwoFactorMode }
    });
});

// Reenviar el código por correo si no llegó (solo aplica al modo email).
app.MapPost("/auth/2fa/resend", async (ResendRequest req, CatalogDbContext catalog,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var viejo = await catalog.TwoFactorChallenges.FirstOrDefaultAsync(c => c.Id == req.ChallengeId);
    if (viejo is null || viejo.UsedAt is not null || viejo.Mode != "email")
        return Results.Ok(new { message = "Si el reto sigue vigente, te reenviamos el código." });
    var user = await catalog.Users.FindAsync(viejo.UserId);
    if (user is null) return Results.Ok(new { message = "Si el reto sigue vigente, te reenviamos el código." });

    var nuevo = await DosFactores.AbrirRetoAsync(catalog, user, viejo.Purpose, email, logs, cfg);
    return Results.Ok(new { challengeId = nuevo.Id, message = "Te reenviamos el código." });
});

// ---------- Alta y baja del segundo factor (usuario autenticado) ----------
app.MapGet("/me/2fa", async (ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    return Results.Ok(new { mode = user.TwoFactorMode, confirmed = user.TwoFactorConfirmedAt is not null });
}).RequireAuthorization();

// Paso 1 del alta. Con app: devuelve el secreto para escribirlo o escanearlo.
// Con correo: manda un código de prueba al correo del usuario.
app.MapPost("/me/2fa/setup", async (TwoFactorSetupRequest req, ITenantContext tc, CatalogDbContext catalog,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    var modo = (req.Mode ?? "").Trim().ToLowerInvariant();

    if (modo == "totp")
    {
        user.TotpSecret = Totp.NuevoSecreto();   // aún no queda activo: falta confirmar
        await catalog.SaveChangesAsync();
        return Results.Ok(new
        {
            mode = "totp",
            secret = user.TotpSecret,
            uri = Totp.UriDeConfiguracion("Aprendor", user.Email, user.TotpSecret)
        });
    }
    if (modo == "email")
    {
        var reto = await DosFactores.AbrirRetoAsync(catalog, user, "enroll", email, logs, cfg, modoForzado: "email");
        return Results.Ok(new { mode = "email", challengeId = reto.Id });
    }
    return Results.BadRequest("Modo inválido: usa \"totp\" o \"email\".");
}).RequireAuthorization();

// Paso 2 del alta: confirmar con un código real antes de exigirlo en el próximo login.
app.MapPost("/me/2fa/confirm", async (TwoFactorConfirmRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    var modo = (req.Mode ?? "").Trim().ToLowerInvariant();

    if (modo == "totp")
    {
        if (!Totp.Verificar(user.TotpSecret, req.Code))
            return Results.BadRequest("El código no coincide. Revisa la hora del teléfono y vuelve a intentar.");
    }
    else if (modo == "email")
    {
        var (ok, _, error) = await DosFactores.ConsumirAsync(catalog, req.ChallengeId ?? Guid.Empty, req.Code, "enroll");
        if (!ok) return Results.BadRequest(error);
    }
    else return Results.BadRequest("Modo inválido.");

    user.TwoFactorMode = modo;
    user.TwoFactorConfirmedAt = DateTime.UtcNow;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-enabled", Detail = $"{user.Email} ({modo})", UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { mode = user.TwoFactorMode, confirmed = true });
}).RequireAuthorization();

// Baja: pide la contraseña actual, para que no baste con una sesión abierta ajena.
app.MapPost("/me/2fa/disable", async (DisableTwoFactorRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (!PasswordHasher.Verify(req.CurrentPassword ?? "", user.PasswordHash))
        return Results.BadRequest("La contraseña actual no es correcta.");

    user.TwoFactorMode = "none";
    user.TotpSecret = null;
    user.TwoFactorConfirmedAt = null;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-disabled", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { mode = "none", confirmed = false });
}).RequireAuthorization();

// ---------- Recuperación de contraseña desde el login ----------
// Pedir el enlace. Responde SIEMPRE lo mismo exista o no el correo: si dijéramos
// "ese correo no existe" estaríamos regalando una lista de usuarios válidos.
app.MapPost("/auth/forgot-password", async (ForgotPasswordRequest req, HttpRequest http,
    CatalogDbContext catalog, IEmailSender email, IConfiguration cfg, ILoggerFactory logs) =>
{
    var generico = Results.Ok(new { message = "Si el correo está registrado, te enviamos un enlace para restablecer la contraseña." });
    var correo = (req.Email ?? "").Trim();
    if (correo.Length == 0) return generico;

    var user = await catalog.Users.FirstOrDefaultAsync(u => u.Email == correo);
    if (user is null) return generico;

    var ahora = DateTime.UtcNow;

    // Freno anti-bombardeo: si ya se pidió uno hace menos de dos minutos, no se manda otro.
    var reciente = await catalog.PasswordResetTokens.AnyAsync(t =>
        t.UserId == user.Id && t.UsedAt == null && t.CreatedAt > ahora.AddMinutes(-2));
    if (reciente) return generico;

    // Un enlace vivo a la vez: los anteriores sin usar quedan invalidados.
    var previos = await catalog.PasswordResetTokens
        .Where(t => t.UserId == user.Id && t.UsedAt == null).ToListAsync();
    catalog.PasswordResetTokens.RemoveRange(previos);

    var (token, hash) = ResetTokens.Create();
    catalog.PasswordResetTokens.Add(new PasswordResetToken
    {
        UserId = user.Id,
        TokenHash = hash,
        CreatedAt = ahora,
        ExpiresAt = ahora.AddMinutes(ResetTokens.VigenciaMinutos)
    });
    await catalog.SaveChangesAsync();

    var baseUrl = (cfg["App:BaseUrl"] ?? "").TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{http.Scheme}://{http.Host}";
    var enlace = $"{baseUrl}/index.html?reset={token}";

    try
    {
        await email.SendAsync(user.Email, user.Name, "Restablecer tu contraseña de Aprendor",
            EmailTemplates.PasswordReset(user.Name, enlace, ResetTokens.VigenciaMinutos));
    }
    catch (Exception ex)
    {
        logs.CreateLogger("PasswordReset").LogWarning(ex, "No se pudo enviar el correo de recuperación a {Email}", user.Email);
    }
    // Sin SMTP configurado no hay forma de entregar el enlace, así que queda en el log
    // para que un administrador pueda hacérselo llegar. Con SMTP configurado NUNCA se
    // escribe el enlace en el log.
    if (string.IsNullOrWhiteSpace(cfg["Email:Host"]))
        logs.CreateLogger("PasswordReset").LogWarning(
            "Email:Host no está configurado: no se envió correo. Enlace de recuperación para {Email}: {Enlace}",
            user.Email, enlace);

    return generico;
});

// Consumir el enlace y fijar la contraseña nueva.
app.MapPost("/auth/reset-password", async (ResetWithTokenRequest req, CatalogDbContext catalog) =>
{
    if ((req.NewPassword ?? "").Length < 8)
        return Results.BadRequest("La nueva contraseña debe tener al menos 8 caracteres.");

    var hash = ResetTokens.Hash(req.Token ?? "");
    var ahora = DateTime.UtcNow;
    var registro = await catalog.PasswordResetTokens
        .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > ahora);
    if (registro is null)
        return Results.BadRequest("El enlace no es válido o ya venció. Pide uno nuevo desde el login.");

    var user = await catalog.Users.FindAsync(registro.UserId);
    if (user is null) return Results.BadRequest("El enlace no es válido.");

    user.PasswordHash = PasswordHasher.Hash(req.NewPassword!);
    user.MustChangePassword = false;   // la acaba de escoger el propio usuario
    registro.UsedAt = ahora;

    // Cualquier otro enlace pendiente de este usuario deja de servir.
    var otros = await catalog.PasswordResetTokens
        .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != registro.Id).ToListAsync();
    catalog.PasswordResetTokens.RemoveRange(otros);

    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "password-reset", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { message = "Contraseña actualizada. Ya puedes entrar." });
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

// Retos de segundo factor: alta, envío del código y consumo.
static class DosFactores
{
    public const int VigenciaMinutos = 10;
    public const int MaxIntentos = 5;

    public static async Task<TwoFactorChallenge> AbrirRetoAsync(CatalogDbContext catalog, AppUser user,
        string proposito, IEmailSender email, ILoggerFactory logs, IConfiguration cfg, string? modoForzado = null)
    {
        var modo = modoForzado ?? user.TwoFactorMode;
        var ahora = DateTime.UtcNow;

        // Un reto vivo a la vez por usuario.
        var previos = await catalog.TwoFactorChallenges
            .Where(c => c.UserId == user.Id && c.UsedAt == null).ToListAsync();
        catalog.TwoFactorChallenges.RemoveRange(previos);

        string? codigo = null, hash = null;
        if (modo == "email") { codigo = OtpCodigos.Nuevo(); hash = OtpCodigos.Hash(codigo); }

        var reto = new TwoFactorChallenge
        {
            UserId = user.Id,
            Mode = modo,
            Purpose = proposito,
            CodeHash = hash,
            CreatedAt = ahora,
            ExpiresAt = ahora.AddMinutes(VigenciaMinutos)
        };
        catalog.TwoFactorChallenges.Add(reto);
        await catalog.SaveChangesAsync();

        if (modo == "email" && codigo is not null)
        {
            try
            {
                await email.SendAsync(user.Email, user.Name, "Tu código de verificación de Aprendor",
                    EmailTemplates.TwoFactorCode(user.Name, codigo, VigenciaMinutos));
            }
            catch (Exception ex)
            {
                logs.CreateLogger("DosFactores").LogWarning(ex, "No se pudo enviar el código a {Email}", user.Email);
            }
            // Igual que con la recuperación: sin SMTP no hay forma de entregarlo.
            if (string.IsNullOrWhiteSpace(cfg["Email:Host"]))
                logs.CreateLogger("DosFactores").LogWarning(
                    "Email:Host no está configurado: código de verificación para {Email}: {Codigo}", user.Email, codigo);
        }
        return reto;
    }

    public static async Task<(bool ok, AppUser? user, string? error)> ConsumirAsync(
        CatalogDbContext catalog, Guid challengeId, string? codigo, string proposito)
    {
        var ahora = DateTime.UtcNow;
        var reto = await catalog.TwoFactorChallenges.FirstOrDefaultAsync(c => c.Id == challengeId);
        if (reto is null || reto.UsedAt is not null || reto.Purpose != proposito)
            return (false, null, "El código no es válido. Vuelve a iniciar sesión.");
        if (reto.ExpiresAt <= ahora)
            return (false, null, "El código venció. Pide uno nuevo.");
        if (reto.Attempts >= MaxIntentos)
            return (false, null, "Demasiados intentos. Vuelve a iniciar sesión.");

        var user = await catalog.Users.FindAsync(reto.UserId);
        if (user is null) return (false, null, "El código no es válido.");

        reto.Attempts++;
        bool valido = reto.Mode == "totp"
            ? Totp.Verificar(user.TotpSecret, codigo)
            : reto.CodeHash is not null && !string.IsNullOrWhiteSpace(codigo) &&
              CryptographicOperations.FixedTimeEquals(
                  Encoding.UTF8.GetBytes(OtpCodigos.Hash(codigo!)), Encoding.UTF8.GetBytes(reto.CodeHash));

        if (!valido)
        {
            await catalog.SaveChangesAsync();
            var quedan = Math.Max(0, MaxIntentos - reto.Attempts);
            return (false, null, $"Código incorrecto. Te quedan {quedan} intento(s).");
        }

        reto.UsedAt = ahora;
        await catalog.SaveChangesAsync();
        return (true, user, null);
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
record ForgotPasswordRequest(string Email);
record ResetWithTokenRequest(string Token, string NewPassword);
record TwoFactorVerifyRequest(Guid ChallengeId, string Code);
record ResendRequest(Guid ChallengeId);
record TwoFactorSetupRequest(string Mode);
record TwoFactorConfirmRequest(string Mode, string Code, Guid? ChallengeId);
record DisableTwoFactorRequest(string CurrentPassword);
record StatusRequest(string Status);
