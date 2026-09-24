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
// Además de las variables de entorno sin prefijo que ya carga CreateBuilder, admite
// las mismas claves con el prefijo APRENDOR_ (p. ej. APRENDOR_Email__ApiKey), para
// no chocar con otras variables de entorno en una máquina que corre varios proyectos.
builder.Configuration.AddEnvironmentVariables(prefix: "APRENDOR_");
var cfg = builder.Configuration;

// Bitácora en texto plano en App_Data\logs\app-log.txt — para ver errores reales
// (como un envío de correo que falla) sin depender de cómo esté hospedada la app.
builder.Logging.AddProvider(new SimpleFileLoggerProvider(
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "logs", "app-log.txt")));

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
// Vía la API HTTP de Brevo (Email:ApiKey) — no SMTP, no hace falta una SMTP key aparte.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmailSender, BrevoApiEmailSender>();
builder.Services.AddHostedService<WeeklyDigestService>();
builder.Services.AddHostedService<ReminderService>();

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

// Origen de la petición en curso: con App:BaseUrl vacío, el enlace del certificado
// (/c/{token}) que sale por correo se arma con el host por el que entró la petición.
app.Use(async (ctx, next) =>
{
    CertificateLinks.OrigenPeticion = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}";
    await next();
});

// ---------- Auth ----------
app.MapPost("/auth/login", async (LoginRequest req, CatalogDbContext catalog, JwtTokenService jwt,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
    if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var compañias = await Compañias.DeUsuarioAsync(catalog, user);
    var politica = await Compañias.PoliticaAsync(catalog, user, user.TenantId);
    var tiene2fa = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;

    // La compañía decide si se usa doble factor. Si lo exige y el usuario no lo tiene
    // configurado, entra con un token válido pero la app lo lleva directo al alta.
    if (politica != "off" && tiene2fa)
    {
        var reto = await DosFactores.AbrirRetoAsync(catalog, user, "login", email, logs, cfg);
        return Results.Ok(new { requires2fa = true, mode = "totp", challengeId = reto.Id });
    }

    return Results.Ok(new
    {
        token = jwt.Create(user),
        user = new { user.Id, user.Email, user.Name, user.Role, user.TenantId, user.MustChangePassword,
                     user.TwoFactorMode, emailVerified = user.EmailVerifiedAt is not null,
                     twoFactorPolicy = politica,
                     mustEnroll2fa = politica == "required" && !tiene2fa,
                     companies = compañias }
    });
});

// Cambiar de compañía sin volver a escribir la contraseña: emite un token nuevo
// para otra compañía a la que el usuario pertenezca.
app.MapPost("/me/switch-company", async (SwitchCompanyRequest req, ITenantContext tc,
    CatalogDbContext catalog, JwtTokenService jwt) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();

    var compañias = await Compañias.DeUsuarioAsync(catalog, user);
    var destino = compañias.FirstOrDefault(c => c.TenantId == req.TenantId);
    if (destino is null) return Results.BadRequest("No perteneces a esa compañía.");

    if (destino.Politica2FA == "required" && !(user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null))
        return Results.BadRequest($"{destino.Nombre} exige verificación en dos pasos. Actívala en tu perfil antes de entrar.");

    return Results.Ok(new
    {
        token = jwt.Create(user, destino.TenantId, destino.Rol),
        user = new { user.Id, user.Email, user.Name, role = destino.Rol, tenantId = destino.TenantId,
                     user.MustChangePassword, user.TwoFactorMode,
                     emailVerified = user.EmailVerifiedAt is not null,
                     twoFactorPolicy = destino.Politica2FA,
                     mustEnroll2fa = false,
                     companies = compañias }
    });
}).RequireAuthorization();

app.MapGet("/me/companies", async (ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    return Results.Ok(await Compañias.DeUsuarioAsync(catalog, user));
}).RequireAuthorization();

// Segundo paso del login: canjear el código por el token.
app.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest req, CatalogDbContext catalog, JwtTokenService jwt) =>
{
    var (ok, user, error) = await DosFactores.ConsumirAsync(catalog, req.ChallengeId, req.Code, "login");
    if (!ok || user is null) return Results.BadRequest(error);

    return Results.Ok(new
    {
        token = jwt.Create(user),
        user = new { user.Id, user.Email, user.Name, user.Role, user.TenantId, user.MustChangePassword,
                     user.TwoFactorMode, emailVerified = user.EmailVerifiedAt is not null }
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

// ---------- Validación del correo del usuario ----------
// Se pide la primera vez que entra. Reusa el mismo mecanismo de retos con código
// del doble factor, con propósito distinto.
app.MapPost("/me/email/send-code", async (ITenantContext tc, CatalogDbContext catalog,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (user.EmailVerifiedAt is not null)
        return Results.Ok(new { verified = true, message = "Tu correo ya está validado." });

    var reto = await DosFactores.AbrirRetoAsync(catalog, user, "verify-email", email, logs, cfg, modoForzado: "email");
    return Results.Ok(new { verified = false, challengeId = reto.Id, email = user.Email });
}).RequireAuthorization();

app.MapPost("/me/email/verify", async (TwoFactorVerifyRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (user.EmailVerifiedAt is not null) return Results.Ok(new { verified = true });

    var (ok, dueño, error) = await DosFactores.ConsumirAsync(catalog, req.ChallengeId, req.Code, "verify-email");
    if (!ok || dueño is null || dueño.Id != user.Id) return Results.BadRequest(error ?? "El código no es válido.");

    user.EmailVerifiedAt = DateTime.UtcNow;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "email-verified", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { verified = true });
}).RequireAuthorization();

// ---------- Alta y baja del segundo factor (usuario autenticado) ----------
app.MapGet("/me/2fa", async (ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    return Results.Ok(new { mode = user.TwoFactorMode, confirmed = user.TwoFactorConfirmedAt is not null });
}).RequireAuthorization();

// Paso 1 del alta: devuelve el secreto para escribirlo o escanearlo en la app
// autenticadora. El segundo factor SOLO se hace con app: el correo no cuenta como
// segundo factor porque suele estar en el mismo dispositivo y con la misma sesión.
app.MapPost("/me/2fa/setup", async (TwoFactorSetupRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    var modo = (req.Mode ?? "totp").Trim().ToLowerInvariant();
    if (modo != "totp")
        return Results.BadRequest("La verificación en dos pasos se hace con app autenticadora.");

    user.TotpSecret = Totp.NuevoSecreto();   // aún no queda activo: falta confirmar
    await catalog.SaveChangesAsync();
    return Results.Ok(new
    {
        mode = "totp",
        secret = user.TotpSecret,
        uri = Totp.UriDeConfiguracion("Aprendor", user.Email, user.TotpSecret)
    });
}).RequireAuthorization();

// Paso 2 del alta: confirmar con un código real antes de exigirlo en el próximo login.
app.MapPost("/me/2fa/confirm", async (TwoFactorConfirmRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();

    if (!Totp.Verificar(user.TotpSecret, req.Code))
        return Results.BadRequest("El código no coincide. Revisa la hora del teléfono y vuelve a intentar.");

    user.TwoFactorMode = "totp";
    user.TwoFactorConfirmedAt = DateTime.UtcNow;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-enabled", Detail = $"{user.Email} (totp)", UserId = user.Id });
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

    // Si alguna de sus compañías lo exige, no puede quitárselo.
    var exigen = (await Compañias.DeUsuarioAsync(catalog, user))
        .Where(c => c.Politica2FA == "required").Select(c => c.Nombre).ToList();
    if (exigen.Count > 0)
        return Results.BadRequest($"No se puede desactivar: {string.Join(", ", exigen)} exige verificación en dos pasos.");

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
    // Sin la API de correo configurada no hay forma de entregar el enlace, así que
    // queda en el log para que un administrador pueda hacérselo llegar. Configurada,
    // NUNCA se escribe el enlace en el log.
    if (string.IsNullOrWhiteSpace(cfg["Email:ApiKey"]))
        logs.CreateLogger("PasswordReset").LogWarning(
            "Email:ApiKey no está configurado: no se envió correo. Enlace de recuperación para {Email}: {Enlace}",
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
    // Llegó hasta aquí por un enlace que solo estaba en su buzón: el correo queda validado.
    user.EmailVerifiedAt ??= ahora;
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
// Vista previa de las plantillas de correo, para revisar cómo se ven sin enviar nada.
// /admin/email-preview?kind=reminder|open|invite|reset|2fa|completion|digest|certificate|certificate-officer
app.MapGet("/admin/email-preview", (string? kind) =>
{
    var k = (kind ?? "reminder").ToLowerInvariant();
    var html = k switch
    {
        "open" => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "open", null, "https://aprendor.advancelogisticspr.com"),
        "invite" => EmailTemplates.Invitation("María Rivera", "maria.rivera@advancelogisticspr.com", "Temporal2026!",
                    "https://aprendor.advancelogisticspr.com"),
        "reset" => EmailTemplates.PasswordReset("María Rivera", "https://aprendor.advancelogisticspr.com/index.html?reset=demo", 60),
        "2fa" => EmailTemplates.TwoFactorCode("María Rivera", "428913", 10),
        "completion" => EmailTemplates.Completion("María Rivera", "Cumplimiento HIPAA para transporte y logística", 270, 300),
        "certificate" or "certificate-officer" => EmailTemplates.CertificateIssued("María Rivera",
                    "Cumplimiento HIPAA para transporte y logística", "CERT-2026-1A2B3C4D", DateTime.UtcNow,
                    DateTime.UtcNow.AddMonths(12), paraArchivo: k == "certificate-officer",
                    "https://aprendor.advancelogisticspr.com", link: "https://aprendor.advancelogisticspr.com/c/demo", dias: 30),
        "digest" => EmailTemplates.Digest("María Rivera",
                    new List<PendingItem> { new(Guid.NewGuid(), "Ética Empresarial y Prevención de Fraude", Guid.NewGuid(), null, "not-started", null) },
                    new List<PendingItem> { new(Guid.NewGuid(), "Seguridad de la Información para Empleados", Guid.NewGuid(), null, "in-progress", null) }),
        _ => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "due15", DateTime.UtcNow.AddDays(15), "https://aprendor.advancelogisticspr.com"),
    };
    return Results.Content(html, "text/html; charset=utf-8");
}).RequireAuthorization("Admin");

// Dispara los recordatorios de inmediato (para probar o forzar un envío).
app.MapPost("/admin/run-reminders", async (IServiceProvider sp, IEmailSender email, IConfiguration configuracion) =>
{
    var n = await ReminderRunner.RunAsync(sp, email, configuracion);
    return Results.Ok(new { sent = n });
}).RequireAuthorization("Admin");

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
            invited = !string.IsNullOrWhiteSpace(cfg["Email:ApiKey"]); // false si el correo no está configurado
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

// Política de doble factor de una compañía: la decide la compañía, no cada usuario.
app.MapPost("/admin/tenants/{id:guid}/two-factor", async (Guid id, TwoFactorPolicyRequest req, CatalogDbContext catalog) =>
{
    var permitidas = new[] { "off", "optional", "required" };
    var p = (req.Policy ?? "").Trim().ToLowerInvariant();
    if (!permitidas.Contains(p)) return Results.BadRequest("Política inválida: off | optional | required.");

    var t = await catalog.Tenants.FindAsync(id);
    if (t is null) return Results.NotFound();
    t.TwoFactorPolicy = p;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-policy", Detail = $"{t.Name} -> {p}" });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { t.Id, t.Name, t.TwoFactorPolicy });
}).RequireAuthorization("Admin");

// Añadir un usuario a otra compañía (con el rol que tendrá allí).
app.MapPost("/admin/user-companies", async (CompanyMembershipRequest req, CatalogDbContext catalog) =>
{
    var roles = new[] { "Admin", "Author", "Moderator", "Learner" };
    if (!roles.Contains(req.Role)) return Results.BadRequest("Rol inválido.");
    var user = await catalog.Users.FindAsync(req.UserId);
    if (user is null) return Results.NotFound("Usuario no encontrado.");
    var tenant = await catalog.Tenants.FindAsync(req.TenantId);
    if (tenant is null) return Results.NotFound("Compañía no encontrada.");
    if (user.TenantId == req.TenantId)
        return Results.BadRequest("Esa ya es su compañía principal.");
    if (await catalog.UserCompanies.AnyAsync(m => m.UserId == req.UserId && m.TenantId == req.TenantId))
        return Results.Conflict("El usuario ya pertenece a esa compañía.");

    catalog.UserCompanies.Add(new UserCompany { UserId = req.UserId, TenantId = req.TenantId, Role = req.Role });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { req.UserId, req.TenantId, req.Role });
}).RequireAuthorization("Admin");

app.MapDelete("/admin/user-companies", async (Guid userId, Guid tenantId, CatalogDbContext catalog) =>
{
    var m = await catalog.UserCompanies.FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId);
    if (m is null) return Results.NotFound();
    catalog.UserCompanies.Remove(m);
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization("Admin");

// Activar / desactivar una compañía.
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

// Pertenencia a compañías. Un usuario tiene una compañía principal (AppUser.TenantId)
// y, opcionalmente, otras en UserCompany. El rol puede ser distinto en cada una.
static class Compañias
{
    public record Membresia(Guid TenantId, string Nombre, string Rol, bool Principal, string Politica2FA);

    public static async Task<List<Membresia>> DeUsuarioAsync(CatalogDbContext catalog, AppUser user)
    {
        var lista = new List<Membresia>();

        if (user.TenantId is Guid principal)
        {
            var t = await catalog.Tenants.FirstOrDefaultAsync(x => x.Id == principal && x.Status == "active");
            if (t is not null) lista.Add(new Membresia(t.Id, t.Name, user.Role, true, t.TwoFactorPolicy));
        }

        var extras = await (from m in catalog.UserCompanies
                            where m.UserId == user.Id
                            join t in catalog.Tenants on m.TenantId equals t.Id
                            where t.Status == "active"
                            select new { t.Id, t.Name, m.Role, t.TwoFactorPolicy }).ToListAsync();

        foreach (var e in extras)
            if (!lista.Any(x => x.TenantId == e.Id))
                lista.Add(new Membresia(e.Id, e.Name, e.Role, false, e.TwoFactorPolicy));

        return lista;
    }

    // La política que aplica al entrar: la de la compañía con la que se va a trabajar.
    // Un admin de plataforma (sin compañía) queda con "optional".
    public static async Task<string> PoliticaAsync(CatalogDbContext catalog, AppUser user, Guid? tenantId)
    {
        var id = tenantId ?? user.TenantId;
        if (id is null) return "optional";
        var p = await catalog.Tenants.Where(t => t.Id == id).Select(t => t.TwoFactorPolicy).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(p) ? "optional" : p;
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
            var esValidacion = proposito == "verify-email";
            try
            {
                await email.SendAsync(user.Email, user.Name,
                    esValidacion ? "Valida tu correo en Aprendor" : "Tu código de verificación de Aprendor",
                    esValidacion
                        ? EmailTemplates.VerifyEmail(user.Name, codigo, VigenciaMinutos)
                        : EmailTemplates.TwoFactorCode(user.Name, codigo, VigenciaMinutos));
            }
            catch (Exception ex)
            {
                logs.CreateLogger("DosFactores").LogWarning(ex, "No se pudo enviar el código a {Email}", user.Email);
            }
            // Igual que con la recuperación: sin la API de correo no hay forma de entregarlo.
            if (string.IsNullOrWhiteSpace(cfg["Email:ApiKey"]))
                logs.CreateLogger("DosFactores").LogWarning(
                    "Email:ApiKey no está configurado: código de verificación para {Email}: {Codigo}", user.Email, codigo);
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
record SwitchCompanyRequest(Guid TenantId);
record TwoFactorPolicyRequest(string Policy);
record CompanyMembershipRequest(Guid UserId, Guid TenantId, string Role);
record DisableTwoFactorRequest(string CurrentPassword);
record StatusRequest(string Status);
