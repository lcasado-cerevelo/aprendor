using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Memory;
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
using TrainingPlatform.Seguridad;
using TrainingPlatform.TenantData;

var builder = WebApplication.CreateBuilder(args);
// Además de las variables de entorno sin prefijo que ya carga CreateBuilder, admite
// las mismas claves con el prefijo APRENDOR_ (p. ej. APRENDOR_Email__ApiKey), para
// no chocar con otras variables de entorno en una máquina que corre varios proyectos.
builder.Configuration.AddEnvironmentVariables(prefix: "APRENDOR_");
var cfg = builder.Configuration;
var esDesarrollo = builder.Environment.IsDevelopment();
var modoMigracion = args.Length > 0 && args[0].Equals("migrate", StringComparison.OrdinalIgnoreCase);

// Arranque seguro: fuera de Development la app NO arranca con la clave JWT de ejemplo
// ni sin la URL pública (los enlaces de los correos se arman solo con ella). El modo
// "migrate" solo toca la base, así que no lo exige.
if (!esDesarrollo && !modoMigracion) ArranqueSeguro.Validar(cfg);

// Bitácora en texto plano en App_Data\logs\app-log-AAAAMMDD.txt (un archivo por día,
// se guardan los últimos 14) — para ver errores reales (como un envío de correo que
// falla) sin depender de cómo esté hospedada la app.
builder.Logging.AddProvider(new SimpleFileLoggerProvider(
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "logs", "app-log.txt")));

// ---- Red: IP real detrás de un proxy, tamaño de petición y cabecera Server ----
// Solo se cree X-Forwarded-For (o CF-Connecting-IP con Cloudflare) si la conexión
// viene de loopback (cloudflared/IIS en la misma máquina) o de una IP listada en
// Security:TrustedProxies. ForwardLimit = 1: solo el salto inmediato.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
    o.KnownProxies.Add(IPAddress.Loopback);
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
    foreach (var p in ArranqueSeguro.Lista(cfg, "Security:TrustedProxies"))
        if (IPAddress.TryParse(p, out var ip)) o.KnownProxies.Add(ip);
    var cabecera = cfg["Security:ForwardedForHeader"];
    if (!string.IsNullOrWhiteSpace(cabecera)) o.ForwardedForHeaderName = cabecera.Trim();
});

// Cuerpo de petición: 1 MB por defecto (Security:MaxRequestBytes). Los endpoints que
// lo necesitan suben su propio límite con RequestSizeLimit (medios, contenido del autor).
var maxPeticion = cfg.GetValue<long?>("Security:MaxRequestBytes") ?? 1_000_000;
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = maxPeticion);
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = maxPeticion;
    o.AddServerHeader = false;
});
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = Limites.MediaMaxBytes(cfg) + MediaTipos.MargenMultipart);

// HSTS (solo fuera de Development). Sin UseHttpsRedirection: detrás del túnel la app
// recibe http y el HTTPS lo fuerza el borde (Cloudflare).
builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));
builder.Services.AddMemoryCache();

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

// Límites por IP real en el acceso (login, 2FA, recuperación, envío de códigos): 429 con
// Retry-After. "¿Olvidaste tu contraseña?" encola el envío y responde sin esperar.
builder.Services.AddLimitesPorIp();
builder.Services.AddSingleton<ColaRestablecer>();
builder.Services.AddHostedService<RestablecerWorker>();

// Correo + resumen semanal (opt-in vía Email:DigestEnabled).
// Vía la API HTTP de Brevo (Email:ApiKey) — no SMTP, no hace falta una SMTP key aparte.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmailSender, BrevoApiEmailSender>();
builder.Services.AddHostedService<WeeklyDigestService>();
builder.Services.AddHostedService<ReminderService>();
// Resumen para los oficiales de cumplimiento, con la cadencia de cada compañía.
builder.Services.AddHostedService<ComplianceDigestService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "sub" / "role" / "tenant_id" as-is
        o.Events = new JwtBearerEvents
        {
            // El token por query (?access_token=) solo se acepta para /media: <img>, <video>
            // y los enlaces de documentos no pueden mandar la cabecera Authorization. En
            // cualquier otra ruta se ignora, para que no acabe en logs ni en el historial.
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/media"))
                {
                    string? t = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(t)) ctx.Token = t;
                }
                return Task.CompletedTask;
            },
            // Sello de seguridad, membresía y rol vigentes contra el catálogo (caché 60 s):
            // cambiar la clave, el 2FA, el rol o quitar una membresía cierra las sesiones.
            OnTokenValidated = Sesiones.ValidarAsync
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

// Autorización:
//  - Por defecto y como respaldo (FallbackPolicy), usuario autenticado con token de
//    alcance completo (scope=full): un endpoint nuevo nace protegido aunque se olvide
//    .RequireAuthorization(). Los anónimos lo declaran con .AllowAnonymous().
//  - "Sesion": cualquier token válido, también los restringidos (cambio de clave,
//    alta del 2FA, validación del correo). Solo /me, /me/password, /me/2fa/* y /me/email/*.
//  - "Admin": rol Admin, de plataforma o de compañía (los endpoints que la usan
//    acotan después a la compañía activa).
//  - "PlatformAdmin": Admin sin compañía, confirmado en el catálogo.
builder.Services.AddScoped<IAuthorizationHandler, PlatformAdminHandler>();
builder.Services.AddAuthorization(o =>
{
    o.DefaultPolicy = PoliticasAcceso.Usuario();
    o.FallbackPolicy = PoliticasAcceso.Usuario();
    o.AddPolicy(PoliticasAcceso.Sesion, p => p.RequireAuthenticatedUser());
    o.AddPolicy("Admin", p => PoliticasAcceso.Base(p).RequireClaim("role", "Admin"));
    o.AddPolicy(AdminPlataforma.Politica, p => PoliticasAcceso.Base(p)
        .RequireClaim("role", "Admin")
        .RequireAssertion(c => !c.User.HasClaim(x => x.Type == "tenant_id"))
        .AddRequirements(new PlatformAdminRequirement()));
});

var app = builder.Build();

// Los enlaces de seguridad (restablecer contraseña, /c/{token}) se arman solo con
// App:BaseUrl fuera de Development; el origen de la petición solo vale en desarrollo.
CertificateLinks.UsarOrigenPeticion = app.Environment.IsDevelopment();
// Los códigos y enlaces que no se pudieron mandar solo van al log en desarrollo.
DosFactores.RegistrarCodigosEnLog = app.Environment.IsDevelopment();

// ---- CLI mode: apply migrations to the catalog + every tenant database ----
if (modoMigracion)
{
    await MigrationRunner.RunAsync(app.Services);
    return;
}

// ---- On startup: migrate catalog and ensure a platform admin exists ----
using (var scope = app.Services.CreateScope())
{
    var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    catalog.Database.Migrate();
    await Bootstrap.SeedAdminAsync(catalog, cfg, app.Environment,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap"));
}

// Primero: la IP y el esquema reales (X-Forwarded-For / CF-Connecting-IP, X-Forwarded-Proto).
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();

// IP de la petición para la auditoría del catálogo (CatalogAuditLog.Ip y SecurityEvent).
app.Use(async (ctx, next) =>
{
    AuditoriaIp.Actual = ClientIp.Of(ctx);
    await next();
});

// Cabeceras de seguridad para todo (estáticos incluidos).
app.Use(async (ctx, next) =>
{
    CabecerasSeguridad.Aplicar(ctx.Response.Headers);
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

// Lo que pasa de aquí no es un archivo estático: respuestas de la API, que llevan datos
// personales y no deben quedar en cachés intermedias ni del navegador.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    await next();
});

// Límites por IP (LimitesPorIp): antes de autenticar, para no gastar nada en quien ya se pasó.
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// Solo en desarrollo: con App:BaseUrl vacío, el enlace del certificado (/c/{token}) que
// sale por correo se arma con el host por el que entró la petición. Fuera de
// Development nunca se usa la cabecera Host para armar enlaces (envenenamiento de Host).
if (app.Environment.IsDevelopment())
    app.Use(async (ctx, next) =>
    {
        CertificateLinks.OrigenPeticion = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}";
        await next();
    });

// ---------- Auth ----------
// Entrar. El mismo 401 exista o no el correo, y el mismo tiempo: si el usuario no existe o
// está bloqueado se verifica contra un hash ficticio. 5 fallos seguidos bloquean la
// cuenta 15 minutos: mientras tanto responde 429 con los minutos que faltan, sin mirar la
// clave. La política de doble factor es la más estricta entre las compañías del usuario.
// Si falta algo (cambio de clave, alta del 2FA exigida, validar el correo) el token es
// restringido (scope) y solo abre /me, /me/password, /me/2fa/* y /me/email/*.
app.MapPost("/auth/login", async (LoginRequest? req, CatalogDbContext catalog, JwtTokenService jwt,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var correo = (req?.Email ?? "").Trim();
    var clave = req?.Password ?? "";
    if (correo.Length == 0 || clave.Length == 0 || correo.Length > 254)
    {
        PasswordHasher.VerifyFicticio(clave);
        return Results.Unauthorized();
    }

    var user = await catalog.Users.FirstOrDefaultAsync(u => u.Email == correo);
    if (user is null)
    {
        PasswordHasher.VerifyFicticio(clave);
        await Bloqueos.FalloLoginDesconocidoAsync(catalog);
        return Results.Unauthorized();
    }

    if (Bloqueos.Espera(user.LockoutEnd) is TimeSpan bloqueada)
    {
        PasswordHasher.VerifyFicticio(clave);
        return Demasiados.Resultado(bloqueada);
    }

    if (!PasswordHasher.Verify(clave, user.PasswordHash))
    {
        var hasta = await Bloqueos.FalloLoginAsync(catalog, user, email, logs.CreateLogger("Login"));
        return hasta is DateTime h ? Demasiados.Resultado(h - DateTime.UtcNow) : Results.Unauthorized();
    }
    await Bloqueos.LimpiarLoginAsync(catalog, user);

    // La contraseña temporal que generó el admin vence a las 72 h.
    if (user.MustChangePassword && user.TempPasswordExpiresAt is DateTime vence && vence <= DateTime.UtcNow)
        return Results.Json(new { error = "La contraseña temporal venció. Usa «¿Olvidaste tu contraseña?» o pide al administrador una nueva." },
            statusCode: StatusCodes.Status401Unauthorized);

    var compañias = await Compañias.DeUsuarioAsync(catalog, user);
    var politica = Compañias.PoliticaEfectiva(compañias);
    var tiene2fa = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;

    // La compañía decide si se usa doble factor. Si lo exige y el usuario no lo tiene
    // configurado, entra con un token restringido (enroll-2fa) que solo sirve para darlo de alta.
    if (politica != "off" && tiene2fa)
    {
        if (Bloqueos.Espera(user.TwoFactorLockedUntil) is TimeSpan espera2fa) return Demasiados.Resultado(espera2fa);
        var reto = await DosFactores.AbrirRetoAsync(catalog, user, "login", email, logs, cfg);
        return Results.Ok(new { requires2fa = true, mode = "totp", challengeId = reto.Id });
    }

    await EventosSeguridad.RegistrarAsync(catalog, EventosSeguridad.LoginOk, user.Id, user.TenantId);
    return Results.Ok(await RespuestaSesion.CrearAsync(catalog, jwt, user, Amr.Pwd));
}).AllowAnonymous();

// Cambiar de compañía sin volver a escribir la contraseña: emite un token nuevo para otra
// compañía a la que el usuario pertenezca, que vence cuando vencía el actual (no alarga la
// sesión). Si la compañía destino exige doble factor y la sesión no lo pasó (amr), abre un
// reto y responde requires2fa: el token llega al verificarlo en /auth/2fa/verify.
app.MapPost("/me/switch-company", async (SwitchCompanyRequest req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();

    var compañias = await Compañias.DeUsuarioAsync(catalog, user);
    var destino = compañias.FirstOrDefault(c => c.TenantId == req.TenantId);
    if (destino is null) return Results.BadRequest("No perteneces a esa compañía.");

    var expira = RespuestaSesion.ExpiracionDe(principal);
    var amr = principal.FindFirst("amr")?.Value ?? Amr.Pwd;
    if (destino.Politica2FA == "required")
    {
        if (!(user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null))
            return Results.BadRequest($"{destino.Nombre} exige verificación en dos pasos. Actívala en tu perfil antes de entrar.");
        if (!Amr.EsMfa(amr))
        {
            if (Bloqueos.Espera(user.TwoFactorLockedUntil) is TimeSpan espera) return Demasiados.Resultado(espera);
            var reto = await DosFactores.AbrirRetoAsync(catalog, user, "login", email, logs, cfg,
                destino: destino.TenantId, sesionHasta: expira);
            return Results.Ok(new { requires2fa = true, mode = "totp", challengeId = reto.Id });
        }
    }

    return Results.Ok(await RespuestaSesion.CrearAsync(catalog, jwt, user, amr, destino.TenantId, expira));
}).RequireAuthorization();

app.MapGet("/me/companies", async (ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    return Results.Ok(await Compañias.DeUsuarioAsync(catalog, user));
}).RequireAuthorization();

// Segundo paso del login (o del cambio a una compañía con 2FA obligatorio): canjear el
// código por el token. Cada fallo suma al contador del usuario (todos los retos): a los
// 5, el doble factor queda bloqueado 15 minutos.
app.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest req, CatalogDbContext catalog, JwtTokenService jwt,
    IEmailSender email, ILoggerFactory logs) =>
{
    var r = await DosFactores.ConsumirAsync(catalog, req.ChallengeId, req.Code, "login", email, logs.CreateLogger("DosFactores"));
    if (r.Espera is TimeSpan espera) return Demasiados.Resultado(espera, r.Error);
    if (!r.Ok || r.User is null || r.Reto is null) return Results.BadRequest(r.Error);
    var user = r.User;

    if (r.Reto.TargetTenantId is Guid destino)
    {
        // Reto abierto por /me/switch-company: token para esa compañía, sin alargar la sesión.
        if (r.Reto.SessionExpiresAt is not DateTime hasta || hasta <= DateTime.UtcNow.AddSeconds(30))
            return Results.BadRequest("La sesión venció. Vuelve a iniciar sesión.");
        var compañias = await Compañias.DeUsuarioAsync(catalog, user);
        if (!compañias.Any(c => c.TenantId == destino)) return Results.BadRequest("No perteneces a esa compañía.");
        await EventosSeguridad.RegistrarAsync(catalog, EventosSeguridad.LoginOk, user.Id, destino);
        return Results.Ok(await RespuestaSesion.CrearAsync(catalog, jwt, user, Amr.Mfa, destino, hasta));
    }

    await EventosSeguridad.RegistrarAsync(catalog, EventosSeguridad.LoginOk, user.Id, user.TenantId);
    return Results.Ok(await RespuestaSesion.CrearAsync(catalog, jwt, user, Amr.Mfa));
}).AllowAnonymous();

// Reenviar el código por correo si no llegó (solo retos de correo vigentes). Por cuenta:
// 60 s entre envíos y 10 al día.
app.MapPost("/auth/2fa/resend", async (ResendRequest req, CatalogDbContext catalog,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var generico = Results.Ok(new { message = "Si el reto sigue vigente, te reenviamos el código." });
    var viejo = await catalog.TwoFactorChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ChallengeId);
    if (viejo is null || viejo.UsedAt is not null || viejo.Mode != "email" || viejo.ExpiresAt <= DateTime.UtcNow)
        return generico;
    var user = await catalog.Users.FindAsync(viejo.UserId);
    if (user is null) return generico;
    if (await Bloqueos.EsperaEnvioCodigoAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);

    var nuevo = await DosFactores.AbrirRetoAsync(catalog, user, viejo.Purpose, email, logs, cfg, modoForzado: "email",
        destino: viejo.TargetTenantId, sesionHasta: viejo.SessionExpiresAt);
    return Results.Ok(new { challengeId = nuevo.Id, message = "Te reenviamos el código." });
}).AllowAnonymous();

// ---------- Validación del correo del usuario ----------
// Se pide la primera vez que entra (token restringido verify-email). Reusa el mecanismo
// de retos con código del doble factor, con propósito distinto. Al validarlo se devuelve
// un token nuevo con el alcance que corresponda.
app.MapPost("/me/email/send-code", async (ITenantContext tc, ClaimsPrincipal principal, CatalogDbContext catalog,
    JwtTokenService jwt, IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (user.EmailVerifiedAt is not null)
    {
        var s = await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal);
        return Results.Ok(new { verified = true, message = "Tu correo ya está validado.", s.Token, s.Scope, s.User });
    }
    if (await Bloqueos.EsperaEnvioCodigoAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);

    var reto = await DosFactores.AbrirRetoAsync(catalog, user, "verify-email", email, logs, cfg, modoForzado: "email");
    return Results.Ok(new { verified = false, challengeId = reto.Id, email = user.Email });
}).RequireAuthorization(PoliticasAcceso.Sesion);

app.MapPost("/me/email/verify", async (TwoFactorVerifyRequest req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IEmailSender email, ILoggerFactory logs) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (user.EmailVerifiedAt is null)
    {
        var r = await DosFactores.ConsumirAsync(catalog, req.ChallengeId, req.Code, "verify-email", email,
            logs.CreateLogger("DosFactores"), dueño: user.Id);
        if (r.Espera is TimeSpan espera) return Demasiados.Resultado(espera, r.Error);
        if (!r.Ok) return Results.BadRequest(r.Error ?? "El código no es válido.");

        user.EmailVerifiedAt = DateTime.UtcNow;
        catalog.AuditLogs.Add(new CatalogAuditLog { Action = "email-verified", Detail = user.Email, UserId = user.Id });
        await catalog.SaveChangesAsync();
    }
    var s = await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal);
    return Results.Ok(new { verified = true, s.Token, s.Scope, s.User });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// ---------- Alta, cambio y baja del segundo factor (usuario autenticado) ----------
app.MapGet("/me/2fa", async (ITenantContext tc, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    return Results.Ok(new { mode = user.TwoFactorMode, confirmed = user.TwoFactorConfirmedAt is not null,
                            pending = user.PendingTotpSecret is not null });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// Paso 1 del alta (o del cambio de autenticador): devuelve un secreto nuevo para
// escribirlo o escanearlo en la app. Queda PENDIENTE (PendingTotpSecret): el vigente sigue
// valiendo hasta confirmar. Si ya hay 2FA confirmado, cambiarlo exige la contraseña actual
// y un código válido del autenticador actual (quien solo tiene la sesión abierta no puede
// registrar el suyo). El segundo factor SOLO se hace con app: el correo no cuenta como
// segundo factor porque suele estar en el mismo dispositivo y con la misma sesión.
app.MapPost("/me/2fa/setup", async (TwoFactorSetupRequest? req, ITenantContext tc, CatalogDbContext catalog,
    IEmailSender email, ILoggerFactory logs) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    var modo = (req?.Mode ?? "totp").Trim().ToLowerInvariant();
    if (modo != "totp")
        return Results.BadRequest("La verificación en dos pasos se hace con app autenticadora.");

    var confirmado = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;
    if (confirmado)
    {
        if (await Bloqueos.EsperaSensibleAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);
        if (!PasswordHasher.Verify(req?.CurrentPassword, user.PasswordHash))
        {
            await Bloqueos.FalloSensibleAsync(catalog, user.Id);
            return Results.BadRequest("La contraseña actual no es correcta.");
        }
        Bloqueos.OkSensible(catalog, user.Id);
        if (Bloqueos.Espera(user.TwoFactorLockedUntil) is TimeSpan espera2fa)
        {
            await catalog.SaveChangesAsync();
            return Demasiados.Resultado(espera2fa);
        }
        var paso = Totp.Verificar(user.TotpSecret, req?.Code);
        if (paso is not long p || p <= (user.LastTotpStep ?? long.MinValue) || !await DosFactores.MarcarPasoAsync(catalog, user.Id, p))
        {
            var hasta = await Bloqueos.Fallo2faAsync(catalog, user, email, logs.CreateLogger("DosFactores"));
            if (hasta is DateTime h) return Demasiados.Resultado(h - DateTime.UtcNow);
            return Results.BadRequest("El código de tu app autenticadora actual no es válido.");
        }
        await Bloqueos.Limpiar2faAsync(catalog, user.Id);
    }

    user.PendingTotpSecret = Totp.NuevoSecreto();   // aún no queda activo: falta confirmar
    await catalog.SaveChangesAsync();
    return Results.Ok(new
    {
        mode = "totp",
        secret = user.PendingTotpSecret,
        uri = Totp.UriDeConfiguracion("Aprendor", user.Email, user.PendingTotpSecret),
        replacing = confirmado
    });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// Paso 2: confirmar con un código real del secreto pendiente. Solo entonces pasa a ser el
// vigente. Cierra las demás sesiones (sello nuevo), avisa por correo y devuelve un token
// nuevo (amr=mfa: acaba de demostrar que tiene el autenticador).
app.MapPost("/me/2fa/confirm", async (TwoFactorConfirmRequest? req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IMemoryCache cache, IEmailSender email, ILoggerFactory logs) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();

    // Compatibilidad: un alta empezada antes de PendingTotpSecret dejó el secreto en
    // TotpSecret sin confirmar.
    var secreto = user.PendingTotpSecret ?? (user.TwoFactorConfirmedAt is null ? user.TotpSecret : null);
    if (string.IsNullOrWhiteSpace(secreto))
        return Results.BadRequest("Primero genera el código de configuración.");

    if (await Bloqueos.EsperaSensibleAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);
    var paso = Totp.Verificar(secreto, req?.Code);
    if (paso is null)
    {
        await Bloqueos.FalloSensibleAsync(catalog, user.Id);
        return Results.BadRequest("El código no coincide. Revisa la hora del teléfono y vuelve a intentar.");
    }

    var ahora = DateTime.UtcNow;
    var cambio = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;
    user.TotpSecret = secreto;
    user.PendingTotpSecret = null;
    user.TwoFactorMode = "totp";
    user.TwoFactorConfirmedAt = ahora;
    user.LastTotpStep = paso;
    user.TwoFactorFailedCount = 0;
    user.TwoFactorLockedUntil = null;
    Sesiones.Rotar(user, cache);
    Bloqueos.OkSensible(catalog, user.Id);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = cambio ? "2fa-changed" : "2fa-enabled", Detail = $"{user.Email} (totp)", UserId = user.Id });
    await catalog.SaveChangesAsync();

    try
    {
        await email.SendAsync(user.Email, user.Name,
            cambio ? "Cambiaste tu app autenticadora de Aprendor" : "Activaste la verificación en dos pasos en Aprendor",
            EmailTemplates.AuthenticatorChanged(user.Name, cambio ? "changed" : "enabled", ahora));
    }
    catch (Exception ex) { logs.CreateLogger("DosFactores").LogWarning(ex, "No se pudo avisar del cambio de 2FA a {Email}", user.Email); }

    var s = await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal, Amr.Mfa);
    return Results.Ok(new { mode = user.TwoFactorMode, confirmed = true, s.Token, s.Scope, s.User });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// Baja: pide la contraseña actual, para que no baste con una sesión abierta ajena.
app.MapPost("/me/2fa/disable", async (DisableTwoFactorRequest? req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IMemoryCache cache, IEmailSender email, ILoggerFactory logs) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (await Bloqueos.EsperaSensibleAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);
    if (!PasswordHasher.Verify(req?.CurrentPassword, user.PasswordHash))
    {
        await Bloqueos.FalloSensibleAsync(catalog, user.Id);
        return Results.BadRequest("La contraseña actual no es correcta.");
    }
    Bloqueos.OkSensible(catalog, user.Id);

    // Si alguna de sus compañías lo exige, no puede quitárselo.
    var exigen = (await Compañias.DeUsuarioAsync(catalog, user))
        .Where(c => c.Politica2FA == "required").Select(c => c.Nombre).ToList();
    if (exigen.Count > 0)
    {
        await catalog.SaveChangesAsync();
        return Results.BadRequest($"No se puede desactivar: {string.Join(", ", exigen)} exige verificación en dos pasos.");
    }

    var teniaAlgo = user.TwoFactorMode != "none" || user.TotpSecret is not null;
    user.TwoFactorMode = "none";
    user.TotpSecret = null;
    user.PendingTotpSecret = null;
    user.TwoFactorConfirmedAt = null;
    user.LastTotpStep = null;
    Sesiones.Rotar(user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-disabled", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();

    if (teniaAlgo)
        try
        {
            await email.SendAsync(user.Email, user.Name, "Desactivaste la verificación en dos pasos en Aprendor",
                EmailTemplates.AuthenticatorChanged(user.Name, "disabled", DateTime.UtcNow));
        }
        catch (Exception ex) { logs.CreateLogger("DosFactores").LogWarning(ex, "No se pudo avisar de la baja de 2FA a {Email}", user.Email); }

    var s = await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal, Amr.Pwd);
    return Results.Ok(new { mode = "none", confirmed = false, s.Token, s.Scope, s.User });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// ---------- Recuperación de contraseña desde el login ----------
// Pedir el enlace. Responde SIEMPRE lo mismo y en el mismo tiempo, exista o no el correo:
// en los dos casos se busca el usuario, se cuentan sus solicitudes y se anota un
// SecurityEvent; el token y el correo se hacen en segundo plano (RestablecerWorker). Por
// cuenta: 3 por hora y 5 al día (las de más no hacen nada).
app.MapPost("/auth/forgot-password", async (ForgotPasswordRequest? req, HttpRequest http, CatalogDbContext catalog,
    ColaRestablecer cola, IConfiguration cfg, ILoggerFactory logs, IWebHostEnvironment env) =>
{
    var generico = Results.Ok(new { message = "Si el correo está registrado, te enviamos un enlace para restablecer la contraseña." });
    var correo = (req?.Email ?? "").Trim();
    if (correo.Length == 0 || correo.Length > 254) return generico;

    var ahora = DateTime.UtcNow;
    var id = await catalog.Users.AsNoTracking().Where(u => u.Email == correo).Select(u => (Guid?)u.Id).FirstOrDefaultAsync();
    var cuenta = id ?? Guid.Empty;
    var dia = ahora.AddDays(-1);
    var hora = ahora.AddHours(-1);
    var previas = await catalog.SecurityEvents
        .Where(e => e.Kind == EventosSeguridad.Restablecer && e.UserId == cuenta && e.At > dia)
        .Select(e => e.At).ToListAsync();
    var pasado = previas.Count(a => a > hora) >= 3 || previas.Count >= 5;
    EventosSeguridad.Anotar(catalog, pasado ? EventosSeguridad.RestablecerIgnorado : EventosSeguridad.Restablecer, id);
    await catalog.SaveChangesAsync();
    if (id is null || pasado) return generico;

    // Fuera de Development el enlace sale SOLO de App:BaseUrl (el arranque exige que
    // exista y sea https): la cabecera Host la controla quien pide el correo.
    var baseUrl = EnlacesSeguridad.BaseUrl(cfg, http, env);
    if (baseUrl is null)
    {
        logs.CreateLogger("PasswordReset").LogError("App:BaseUrl no está configurado: no se envió el enlace de recuperación.");
        return generico;
    }
    if (!cola.Encolar(new SolicitudRestablecer(id.Value, baseUrl)))
        logs.CreateLogger("PasswordReset").LogError("La cola de restablecimiento está llena: se descartó una solicitud.");
    return generico;
}).AllowAnonymous();

// Consumir el enlace (restablecimiento o invitación) y fijar la contraseña nueva. El
// token es de un solo uso también con peticiones en paralelo (UPDATE condicionado).
app.MapPost("/auth/reset-password", async (ResetWithTokenRequest? req, CatalogDbContext catalog, IMemoryCache cache) =>
{
    var invalido = Results.BadRequest("El enlace no es válido o ya venció. Pide uno nuevo desde el login.");
    var texto = req?.Token ?? "";
    if (texto.Length is 0 or > 128) return invalido;

    var hash = ResetTokens.Hash(texto);
    var ahora = DateTime.UtcNow;
    var registro = await catalog.PasswordResetTokens.AsNoTracking()
        .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > ahora);
    if (registro is null) return invalido;

    var user = await catalog.Users.FindAsync(registro.UserId);
    if (user is null) return invalido;
    if (PoliticaClave.Validar(req!.NewPassword, user.Email) is string error) return Results.BadRequest(error);

    var n = await catalog.PasswordResetTokens.Where(t => t.Id == registro.Id && t.UsedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)ahora));
    if (n == 0) return invalido;

    user.PasswordHash = PasswordHasher.Hash(req.NewPassword!);
    user.MustChangePassword = false;   // la acaba de escoger el propio usuario
    user.TempPasswordExpiresAt = null;
    // Llegó hasta aquí por un enlace que solo estaba en su buzón: el correo queda validado
    // y se levanta el bloqueo por contraseña.
    user.EmailVerifiedAt ??= ahora;
    user.AccessFailedCount = 0;
    user.LockoutEnd = null;
    Sesiones.Rotar(user, cache);

    // Cualquier otro enlace pendiente de este usuario deja de servir.
    var otros = await catalog.PasswordResetTokens
        .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != registro.Id).ToListAsync();
    catalog.PasswordResetTokens.RemoveRange(otros);

    var invitacion = registro.Purpose == "invite";
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = invitacion ? "invite-accepted" : "password-reset", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { message = invitacion ? "Contraseña creada. Ya puedes entrar." : "Contraseña actualizada. Ya puedes entrar." });
}).AllowAnonymous();

// Cambiar la propia contraseña (también limpia el cambio obligatorio). Cierra las demás
// sesiones (sello nuevo) y devuelve un token nuevo para esta.
app.MapPost("/me/password", async (ChangePasswordRequest? req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IMemoryCache cache) =>
{
    if (tc.UserId is null) return Results.Unauthorized();
    var user = await catalog.Users.FindAsync(tc.UserId.Value);
    if (user is null) return Results.NotFound();
    if (PoliticaClave.Validar(req?.NewPassword, user.Email) is string error) return Results.BadRequest(error);

    if (await Bloqueos.EsperaSensibleAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);
    if (!PasswordHasher.Verify(req!.CurrentPassword, user.PasswordHash))
    {
        await Bloqueos.FalloSensibleAsync(catalog, user.Id);
        return Results.BadRequest("La contraseña actual no es correcta.");
    }
    if (req.NewPassword == req.CurrentPassword)
        return Results.BadRequest("La nueva contraseña tiene que ser distinta de la actual.");

    user.PasswordHash = PasswordHasher.Hash(req.NewPassword!);
    user.MustChangePassword = false;
    user.TempPasswordExpiresAt = null;
    Sesiones.Rotar(user, cache);
    Bloqueos.OkSensible(catalog, user.Id);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "password-changed", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Results.Ok(await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal));
}).RequireAuthorization(PoliticasAcceso.Sesion);

// isComplianceOfficer: la marca de oficial de cumplimiento en la compañía activa
// (se consulta en el catálogo, no va en el token: quitarla surte efecto al instante).
// scope/amr: el alcance y cómo se autenticó la sesión (sirve también con tokens restringidos).
app.MapGet("/me", async (ITenantContext tc, ClaimsPrincipal principal, CatalogDbContext catalog) =>
    Results.Ok(new { tc.UserId, tc.TenantId, tc.Role,
                     scope = principal.FindFirst("scope")?.Value,
                     amr = principal.FindFirst("amr")?.Value,
                     isComplianceOfficer = await ComplianceOfficers.EsOficialAsync(catalog, tc.UserId, tc.TenantId) }))
    .RequireAuthorization(PoliticasAcceso.Sesion);

// ---------- Admin: tenants & users ----------
// Compañías, procesos globales y membresías: solo el admin de plataforma
// (política PlatformAdmin). Usuarios: también el Admin de una compañía, acotado a la
// suya (ver AdminUsuarios).

// Si el servidor tiene Tenants:ConnectionTemplate (con {db}), la cadena de conexión se
// arma aquí con el nombre de base validado y se ignora cualquier cadena del cliente.
// Sin plantilla (instalaciones antiguas) se acepta la cadena del admin de plataforma.
app.MapPost("/admin/tenants", async (CreateTenantRequest req, CatalogDbContext catalog,
    IConfiguration config, ILoggerFactory logs) =>
{
    var nombre = (req.Name ?? "").Trim();
    if (nombre.Length < 2 || nombre.Length > 200) return Results.BadRequest("El nombre de la compañía debe tener entre 2 y 200 caracteres.");
    if (await catalog.Tenants.AnyAsync(t => t.Name == nombre))
        return Results.Conflict("Ya existe una compañía con ese nombre.");

    string conexion;
    string? baseDatos = null;
    var plantilla = PlantillaTenant.Leer(config);
    if (plantilla is not null)
    {
        baseDatos = string.IsNullOrWhiteSpace(req.DatabaseName) ? PlantillaTenant.NombrePorDefecto(nombre) : req.DatabaseName.Trim();
        if (!PlantillaTenant.NombreValido(baseDatos))
            return Results.BadRequest("Nombre de base de datos inválido: de 3 a 50 letras, números o guion bajo.");
        conexion = plantilla.Replace("{db}", baseDatos);
        if (await catalog.Tenants.AnyAsync(t => t.ConnectionString == conexion))
            return Results.Conflict("Otra compañía ya usa esa base de datos.");
    }
    else
    {
        if (string.IsNullOrWhiteSpace(req.ConnectionString))
            return Results.BadRequest("Falta la cadena de conexión (el servidor no tiene Tenants:ConnectionTemplate).");
        conexion = req.ConnectionString.Trim();
    }

    var tenant = new Tenant { Name = nombre, ConnectionString = conexion };
    catalog.Tenants.Add(tenant);
    await catalog.SaveChangesAsync();

    // Create + migrate the new tenant's database right away.
    try
    {
        var opts = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(tenant.ConnectionString).Options;
        await using var tdb = new TenantDbContext(opts);
        await tdb.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Sin base no hay compañía: se deshace el alta para poder reintentar.
        logs.CreateLogger("Tenants").LogError(ex, "No se pudo crear la base de datos de la compañía {Nombre}", nombre);
        catalog.Tenants.Remove(tenant);
        await catalog.SaveChangesAsync();
        return Results.Json(new { error = "No se pudo crear la base de datos de la compañía. Revisa el log del servidor." },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "tenant-created", Detail = $"{tenant.Name} ({baseDatos ?? "cadena manual"})" });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { tenant.Id, tenant.Name, databaseName = baseDatos });
}).RequireAuthorization(AdminPlataforma.Politica);

// Lo que la pantalla de alta necesita saber: si el servidor arma la cadena con su
// plantilla (se pide solo el nombre de la base) o si hay que escribirla.
app.MapGet("/admin/tenants/options", (IConfiguration config) =>
    Results.Ok(new
    {
        usesTemplate = PlantillaTenant.Leer(config) is not null,
        databasePrefix = PlantillaTenant.Prefijo,
        databasePattern = PlantillaTenant.Patron
    }))
    .RequireAuthorization(AdminPlataforma.Politica);

app.MapGet("/admin/tenants", async (CatalogDbContext catalog) =>
    Results.Ok(await catalog.Tenants.OrderBy(t => t.Name)
        .Select(t => new { t.Id, t.Name, t.Status, t.TwoFactorPolicy }).ToListAsync()))
    .RequireAuthorization(AdminPlataforma.Politica);

// Dispara el resumen semanal de inmediato (para probar o forzar un envío).
// Vista previa de las plantillas de correo, para revisar cómo se ven sin enviar nada.
// /admin/email-preview?kind=reminder|open|overdue|invite|reset|2fa|locked|2fa-locked|2fa-changed|completion|digest|certificate|certificate-officer|compliance
app.MapGet("/admin/email-preview", (string? kind) =>
{
    var k = (kind ?? "reminder").ToLowerInvariant();
    var html = k switch
    {
        "open" => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "open", null, "https://aprendor.advancelogisticspr.com"),
        "invite" => EmailTemplates.InvitationLink("María Rivera", "maria.rivera@advancelogisticspr.com",
                    "https://aprendor.advancelogisticspr.com/index.html#invite=demo", Invitaciones.VigenciaHoras),
        "locked" => EmailTemplates.AccountLocked("María Rivera", "password", 15),
        "2fa-locked" => EmailTemplates.AccountLocked("María Rivera", "2fa", 15),
        "2fa-changed" => EmailTemplates.AuthenticatorChanged("María Rivera", "changed", DateTime.UtcNow),
        "reset" => EmailTemplates.PasswordReset("María Rivera", "https://aprendor.advancelogisticspr.com/index.html#reset=demo", 60),
        "2fa" => EmailTemplates.TwoFactorCode("María Rivera", "428913", 10),
        "completion" => EmailTemplates.Completion("María Rivera", "Cumplimiento HIPAA para transporte y logística", 270, 300),
        "certificate" or "certificate-officer" => EmailTemplates.CertificateIssued("María Rivera",
                    "Cumplimiento HIPAA para transporte y logística", "CERT-2026-1A2B3C4D", DateTime.UtcNow,
                    DateTime.UtcNow.AddMonths(12), paraArchivo: k == "certificate-officer",
                    "https://aprendor.advancelogisticspr.com", link: "https://aprendor.advancelogisticspr.com/c/demo", dias: 30),
        "overdue" => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "overdue", DateTime.UtcNow.AddDays(-9), "https://aprendor.advancelogisticspr.com"),
        "compliance" => EmailTemplates.ComplianceDigest("Advance Logistics",
                    new List<ComplianceRow> { new(Guid.NewGuid(), "María Rivera", "", new(), new() { "Choferes" }, Guid.NewGuid(),
                        "Cumplimiento HIPAA para transporte y logística", "expired", "overdue", DateTime.UtcNow.AddDays(-3), "expires", 3) },
                    new List<ComplianceRow> { new(Guid.NewGuid(), "José Torres", "", new(), new() { "Almacén" }, Guid.NewGuid(),
                        "Hostigamiento sexual en el empleo", "overdue", "overdue", DateTime.UtcNow.AddDays(-20), "due", 20) },
                    new List<ComplianceRow> { new(Guid.NewGuid(), "Ana López", "", new(), new(), Guid.NewGuid(),
                        "Seguridad de la Información para Empleados", "renewal", "due-soon", DateTime.UtcNow.AddDays(12), "expires", 12) },
                    new List<ComplianceRow> { new(Guid.NewGuid(), "Luis Pérez", "", new(), new() { "Nuevos ingresos" }, Guid.NewGuid(),
                        "Ética Empresarial y Prevención de Fraude", "not-started", "not-started", DateTime.UtcNow.AddDays(5), "due", 5) },
                    30, "https://aprendor.advancelogisticspr.com"),
        "digest" => EmailTemplates.Digest("María Rivera",
                    new List<PendingItem> { new(Guid.NewGuid(), "Ética Empresarial y Prevención de Fraude", Guid.NewGuid(), null, "not-started", null) },
                    new List<PendingItem> { new(Guid.NewGuid(), "Seguridad de la Información para Empleados", Guid.NewGuid(), null, "in-progress", null) }),
        _ => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "due15", DateTime.UtcNow.AddDays(15), "https://aprendor.advancelogisticspr.com"),
    };
    return Results.Content(html, "text/html; charset=utf-8");
}).RequireAuthorization(AdminPlataforma.Politica);

// Dispara los recordatorios de inmediato (para probar o forzar un envío). Recorre
// todas las compañías: solo el admin de plataforma.
app.MapPost("/admin/run-reminders", async (IServiceProvider sp, IEmailSender email, IConfiguration configuracion) =>
{
    var n = await ReminderRunner.RunAsync(sp, email, configuracion);
    return Results.Ok(new { sent = n });
}).RequireAuthorization(AdminPlataforma.Politica);

// Dispara el resumen de cumplimiento de inmediato, sin mirar la cadencia ni el marcador
// (para probar o forzar un envío). Respeta la idempotencia de los vencidos: uno ya
// avisado hace poco no se repite. El Admin de una compañía solo corre la suya.
app.MapPost("/admin/run-compliance-digest", async (Guid? tenantId, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache, IServiceProvider sp, IEmailSender email, IConfiguration configuracion) =>
{
    // Sin compañía en el token solo puede ser el admin de plataforma (confirmado en el catálogo).
    if (tc.TenantId is null && !await AdminPlataforma.EsAsync(principal, catalog, cache)) return Results.Forbid();
    var soloTenant = tc.TenantId ?? tenantId;
    var r = await ComplianceDigestRunner.RunAsync(sp, email, configuracion, soloTenant);
    return Results.Ok(new
    {
        ran = true,
        sent = r.Sum(x => x.Enviados),
        tenants = r.Select(x => new
        {
            tenantId = x.TenantId, tenant = x.Tenant, recipients = x.Destinatarios, sent = x.Enviados,
            newOverdue = x.Nuevos, stillOverdue = x.Siguen, dueSoon = x.PorVencer, notStarted = x.SinComenzar,
            skipped = x.Omitido
        })
    });
}).RequireAuthorization("Admin");

app.MapPost("/admin/run-digest", async (IServiceProvider sp, IEmailSender email) =>
{
    var sent = await DigestRunner.RunAsync(sp, email);
    return Results.Ok(new { ran = true, sent });
}).RequireAuthorization(AdminPlataforma.Politica);

// Alta de usuario. El admin de plataforma lo crea en cualquier compañía (o sin
// compañía); el Admin de una compañía solo en la suya (otro tenantId -> 403).
// Ninguna contraseña viaja por correo:
//  - sendInvite = true: la cuenta nace sin clave y se manda un enlace de un solo uso
//    (#invite=TOKEN, 72 h) para que la persona cree la suya.
//  - sendInvite = false: el servidor genera una clave temporal de 16 caracteres, la
//    devuelve UNA sola vez (temporaryPassword) y vence a las 72 h; al entrar hay que
//    cambiarla. La "password" que mande el cliente se ignora.
app.MapPost("/admin/users", async (CreateUserRequest req, HttpRequest http, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache, IEmailSender email, IConfiguration cfg, IWebHostEnvironment env,
    ILoggerFactory logs) =>
{
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();

    var correo = (req.Email ?? "").Trim();
    if (correo.Length is 0 or > 254 || !correo.Contains('@')) return Results.BadRequest("Correo inválido.");
    if (!AdminUsuarios.Roles.Contains(req.Role)) return Results.BadRequest("Rol inválido.");

    var compañia = req.TenantId;
    if (alcance.Compañia is Guid suya)
    {
        if (compañia is not null && compañia != suya) return Results.Forbid();
        compañia = suya;
    }
    else if (compañia is not null && !await catalog.Tenants.AnyAsync(t => t.Id == compañia))
        return Results.BadRequest("Compañía no encontrada.");

    if (await catalog.Users.AnyAsync(u => u.Email == correo))
        return Results.Conflict("Ya existe un usuario con ese correo.");

    var invitar = req.SendInvite == true;
    var temporal = invitar ? null : PoliticaClave.Temporal();
    var user = new AppUser
    {
        Email = correo,
        Name = (req.Name ?? "").Trim(),
        Role = req.Role,
        TenantId = compañia,
        // Invitado: sin clave utilizable hasta que use el enlace. Con temporal: obliga a
        // cambiarla al primer login y vence a las 72 h.
        MustChangePassword = !invitar,
        TempPasswordExpiresAt = invitar ? null : DateTime.UtcNow.Add(PoliticaClave.VigenciaTemporal),
        PasswordHash = invitar ? "" : PasswordHasher.Hash(temporal!)
    };
    catalog.Users.Add(user);
    catalog.AuditLogs.Add(new CatalogAuditLog
    {
        Action = "user-created",
        Detail = $"{user.Email} ({user.Role}, {(invitar ? "invitación" : "clave temporal")})",
        UserId = tc.UserId
    });
    await catalog.SaveChangesAsync();

    bool invited = false;
    if (invitar)
        invited = await Invitaciones.EnviarAsync(catalog, user, EnlacesSeguridad.BaseUrl(cfg, http, env),
            email, cfg, env, logs.CreateLogger("Invitaciones"));
    return Results.Ok(new
    {
        user.Id, user.Email, user.Role, user.TenantId, invited,
        temporaryPassword = temporal,
        temporaryPasswordExpiresAt = user.TempPasswordExpiresAt
    });
}).RequireAuthorization("Admin");

// Listar usuarios (opcionalmente filtrados por cliente). El Admin de una compañía ve
// solo la suya (principal o por UserCompany), con el rol que tienen ahí, sin admins de
// plataforma y sin sus membresías en otras compañías.
app.MapGet("/admin/users", async (Guid? tenantId, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache) =>
{
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();

    if (alcance.Compañia is Guid suya)
    {
        if (tenantId is not null && tenantId != suya) return Results.Forbid();
        var extras = catalog.UserCompanies.Where(m => m.TenantId == suya).Select(m => m.UserId);
        var gente = await catalog.Users
            .Where(u => (u.TenantId == suya || extras.Contains(u.Id)) && !(u.TenantId == null && u.Role == "Admin"))
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword }).ToListAsync();
        var ids = gente.Select(u => u.Id).ToList();
        var aqui = await catalog.UserCompanies.Where(m => m.TenantId == suya && ids.Contains(m.UserId))
            .Select(m => new { m.Id, m.UserId, m.TenantId, m.Role, m.IsComplianceOfficer }).ToListAsync();

        return Results.Ok(gente.Select(u =>
        {
            var m = aqui.FirstOrDefault(x => x.UserId == u.Id);
            var principalAqui = u.TenantId == suya;
            return new
            {
                u.Id, u.Email, u.Name,
                role = principalAqui ? u.Role : (m?.Role ?? u.Role),
                tenantId = (Guid?)suya,
                u.MustChangePassword,
                isComplianceOfficer = m?.IsComplianceOfficer == true,
                memberships = m is null ? Array.Empty<object>() : new object[]
                {
                    new { membershipId = m.Id, tenantId = m.TenantId, role = principalAqui ? u.Role : m.Role,
                          principal = principalAqui, isComplianceOfficer = m.IsComplianceOfficer }
                }
            };
        }));
    }

    var q = catalog.Users.AsQueryable();
    if (tenantId is not null) q = q.Where(u => u.TenantId == tenantId);
    var users = await q.OrderBy(u => u.Name)
        .Select(u => new { u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword })
        .ToListAsync();

    // Membresías (UserCompany) de esos usuarios, con la marca de oficial de cumplimiento.
    var todos = users.Select(u => u.Id).ToList();
    var membresias = await catalog.UserCompanies.Where(m => todos.Contains(m.UserId))
        .Select(m => new { m.Id, m.UserId, m.TenantId, m.Role, m.IsComplianceOfficer }).ToListAsync();

    return Results.Ok(users.Select(u =>
    {
        var compañia = tenantId ?? u.TenantId;
        var suyas = membresias.Where(m => m.UserId == u.Id).ToList();
        return new
        {
            u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword,
            // Oficial de cumplimiento en la compañía filtrada (o, sin filtro, en su principal).
            isComplianceOfficer = suyas.Any(m => m.TenantId == compañia && m.IsComplianceOfficer),
            memberships = suyas.Select(m => new
            {
                membershipId = m.Id,
                tenantId = m.TenantId,
                role = m.TenantId == u.TenantId ? u.Role : m.Role,
                principal = m.TenantId == u.TenantId,
                isComplianceOfficer = m.IsComplianceOfficer
            })
        };
    }));
}).RequireAuthorization("Admin");

// Cambiar el rol de un usuario. El Admin de una compañía cambia el rol que la persona
// tiene EN SU compañía: el de la cuenta si es su principal, el de la membresía si no.
app.MapPost("/admin/users/{id:guid}/role", async (Guid id, RoleRequest req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache) =>
{
    if (!AdminUsuarios.Roles.Contains(req.Role)) return Results.BadRequest("Rol inválido.");
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    if (!await AdminUsuarios.PuedeGestionarAsync(alcance, user, catalog)) return Results.Forbid();

    if (alcance.Compañia is Guid suya && user.TenantId != suya)
    {
        var m = await catalog.UserCompanies.FirstAsync(x => x.UserId == id && x.TenantId == suya);
        m.Role = req.Role;
    }
    else
    {
        user.Role = req.Role;
        // La fila "espejo" de la compañía principal (la que guarda la marca de oficial de
        // cumplimiento) sigue el rol de la cuenta.
        foreach (var espejo in await catalog.UserCompanies.Where(m => m.UserId == id && m.TenantId == user.TenantId).ToListAsync())
            espejo.Role = req.Role;
    }
    // Con el rol cambiado, los tokens abiertos (que llevan el rol viejo) dejan de servir.
    Sesiones.Rotar(user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "user-role", Detail = $"{user.Email} -> {req.Role}", UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    Sesiones.Olvidar(cache, id);
    return Results.Ok();
}).RequireAuthorization("Admin");

// Resetear la contraseña de un usuario: el usuario deberá cambiarla al próximo login.
// Sin tempPassword, el servidor genera una de 16 caracteres y la devuelve UNA vez
// (temporaryPassword); si el admin escribe una, debe cumplir la política de claves. La
// temporal vence a las 72 h. Cierra las sesiones abiertas del usuario y levanta su bloqueo.
app.MapPost("/admin/users/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest? req, ITenantContext tc,
    ClaimsPrincipal principal, CatalogDbContext catalog, IMemoryCache cache) =>
{
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    if (!await AdminUsuarios.PuedeGestionarAsync(alcance, user, catalog)) return Results.Forbid();
    // La clave es de la cuenta, no de la membresía: con ella se entra a TODAS las
    // compañías de la persona. El Admin de una compañía solo la fija si la persona no
    // pertenece a ninguna otra (igual que DELETE); si no, lo hace el admin de plataforma.
    if (alcance.Compañia is Guid suya && await AdminUsuarios.TieneOtraCompañiaAsync(user, suya, catalog))
        return Results.Conflict("Esta persona también pertenece a otra compañía; pide al administrador de la plataforma que restablezca su contraseña.");
    var generada = string.IsNullOrWhiteSpace(req?.TempPassword);
    var temporal = generada ? PoliticaClave.Temporal() : req!.TempPassword!;
    if (!generada && PoliticaClave.Validar(temporal, user.Email) is string error) return Results.BadRequest(error);

    user.PasswordHash = PasswordHasher.Hash(temporal);
    user.MustChangePassword = true;
    user.TempPasswordExpiresAt = DateTime.UtcNow.Add(PoliticaClave.VigenciaTemporal);
    user.AccessFailedCount = 0;
    user.LockoutEnd = null;
    Sesiones.Rotar(user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "admin-password-reset", Detail = user.Email, UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { temporaryPassword = generada ? temporal : null, expiresAt = user.TempPasswordExpiresAt });
}).RequireAuthorization("Admin");

// Eliminar un usuario (no puedes eliminarte a ti mismo). El Admin de una compañía no
// borra cuentas que también existen en otra: a quien llega por UserCompany solo lo saca
// de su compañía; a quien la tiene de principal pero pertenece a otras, no lo toca.
app.MapDelete("/admin/users/{id:guid}", async (Guid id, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache) =>
{
    if (tc.UserId == id) return Results.BadRequest("No puedes eliminar tu propio usuario.");
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    if (!await AdminUsuarios.PuedeGestionarAsync(alcance, user, catalog)) return Results.Forbid();

    var membresias = await catalog.UserCompanies.Where(m => m.UserId == id).ToListAsync();
    if (alcance.Compañia is Guid suya)
    {
        if (user.TenantId != suya)
        {
            catalog.UserCompanies.RemoveRange(membresias.Where(m => m.TenantId == suya));
            Sesiones.Rotar(user, cache);   // la baja de la membresía cierra sus sesiones
            catalog.AuditLogs.Add(new CatalogAuditLog { Action = "membership-removed", Detail = user.Email, UserId = tc.UserId });
            await catalog.SaveChangesAsync();
            return Results.Ok(new { removedMembership = true });
        }
        if (membresias.Any(m => m.TenantId != suya))
            return Results.Conflict("Esta persona también pertenece a otra compañía; pide al administrador de la plataforma que la dé de baja.");
    }

    catalog.UserCompanies.RemoveRange(membresias);
    catalog.Users.Remove(user);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "user-deleted", Detail = user.Email, UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    Sesiones.Olvidar(cache, id);   // sin usuario, sus tokens dejan de valer en la próxima petición
    return Results.Ok();
}).RequireAuthorization("Admin");

// Política de doble factor de una compañía: la decide la compañía, no cada usuario.
app.MapPost("/admin/tenants/{id:guid}/two-factor", async (Guid id, TwoFactorPolicyRequest req, ITenantContext tc,
    CatalogDbContext catalog) =>
{
    var permitidas = new[] { "off", "optional", "required" };
    var p = (req.Policy ?? "").Trim().ToLowerInvariant();
    if (!permitidas.Contains(p)) return Results.BadRequest("Política inválida: off | optional | required.");

    var t = await catalog.Tenants.FindAsync(id);
    if (t is null) return Results.NotFound();
    t.TwoFactorPolicy = p;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-policy", Detail = $"{t.Name} -> {p}", UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { t.Id, t.Name, t.TwoFactorPolicy });
}).RequireAuthorization(AdminPlataforma.Politica);

// Añadir un usuario a otra compañía (con el rol que tendrá allí).
app.MapPost("/admin/user-companies", async (CompanyMembershipRequest req, ITenantContext tc, CatalogDbContext catalog,
    IMemoryCache cache) =>
{
    if (!AdminUsuarios.Roles.Contains(req.Role)) return Results.BadRequest("Rol inválido.");
    var user = await catalog.Users.FindAsync(req.UserId);
    if (user is null) return Results.NotFound("Usuario no encontrado.");
    var tenant = await catalog.Tenants.FindAsync(req.TenantId);
    if (tenant is null) return Results.NotFound("Compañía no encontrada.");
    if (user.TenantId == req.TenantId)
        return Results.BadRequest("Esa ya es su compañía principal.");
    if (await catalog.UserCompanies.AnyAsync(m => m.UserId == req.UserId && m.TenantId == req.TenantId))
        return Results.Conflict("El usuario ya pertenece a esa compañía.");

    catalog.UserCompanies.Add(new UserCompany { UserId = req.UserId, TenantId = req.TenantId, Role = req.Role });
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "membership-added", Detail = $"{user.Email} @ {tenant.Name} ({req.Role})", UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    Sesiones.Olvidar(cache, req.UserId);
    return Results.Ok(new { req.UserId, req.TenantId, req.Role });
}).RequireAuthorization(AdminPlataforma.Politica);

// Marcar o desmarcar a un oficial de cumplimiento en una compañía. Cuerpo { "isOfficer": true|false,
// "tenantId": opcional }. {id} es el Id de la membresía (UserCompany); también se acepta el
// Id del usuario, y entonces vale para la compañía de body.tenantId, la del admin que
// llama o la principal del usuario, en ese orden. Si la compañía es su principal y no
// tiene fila en UserCompany, se crea una "espejo" (mismo rol y fecha de alta que la
// cuenta) solo para guardar la marca. El Admin de una compañía solo gestiona la suya.
app.MapPost("/admin/user-companies/{id:guid}/compliance-officer", async (Guid id, ComplianceOfficerRequest req,
    ITenantContext tc, ClaimsPrincipal principal, CatalogDbContext catalog, IMemoryCache cache) =>
{
    // Sin compañía en el token solo puede ser el admin de plataforma (confirmado en el catálogo).
    if (tc.TenantId is null && !await AdminPlataforma.EsAsync(principal, catalog, cache)) return Results.Forbid();
    var m = await catalog.UserCompanies.FirstOrDefaultAsync(x => x.Id == id);
    if (m is null)
    {
        var user = await catalog.Users.FindAsync(id);
        if (user is null) return Results.NotFound("Membresía o usuario no encontrado.");
        var compañia = req.TenantId ?? tc.TenantId ?? user.TenantId;
        if (compañia is null) return Results.BadRequest("Indica la compañía (tenantId).");
        if (tc.TenantId is not null && compañia != tc.TenantId) return Results.Forbid();

        m = await catalog.UserCompanies.FirstOrDefaultAsync(x => x.UserId == user.Id && x.TenantId == compañia);
        if (m is null)
        {
            if (user.TenantId != compañia) return Results.BadRequest("El usuario no pertenece a esa compañía.");
            if (!req.IsOfficer)   // no hay marca que quitar
                return Results.Ok(new { membershipId = (Guid?)null, userId = user.Id, tenantId = compañia, isComplianceOfficer = false });
            m = new UserCompany { UserId = user.Id, TenantId = compañia.Value, Role = user.Role, CreatedAt = user.CreatedAt };
            catalog.UserCompanies.Add(m);
        }
    }
    if (tc.TenantId is not null && m.TenantId != tc.TenantId) return Results.Forbid();

    m.IsComplianceOfficer = req.IsOfficer;
    var datos = await (from u in catalog.Users
                       where u.Id == m.UserId
                       from t in catalog.Tenants.Where(t => t.Id == m.TenantId).DefaultIfEmpty()
                       select new { u.Email, Compañia = t != null ? t.Name : "" }).FirstOrDefaultAsync();
    catalog.AuditLogs.Add(new CatalogAuditLog
    {
        Action = req.IsOfficer ? "compliance-officer-on" : "compliance-officer-off",
        Detail = $"{datos?.Email} @ {datos?.Compañia}",
        UserId = tc.UserId
    });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { membershipId = (Guid?)m.Id, userId = m.UserId, tenantId = (Guid?)m.TenantId, isComplianceOfficer = m.IsComplianceOfficer });
}).RequireAuthorization("Admin");

app.MapDelete("/admin/user-companies", async (Guid userId, Guid tenantId, ITenantContext tc, CatalogDbContext catalog,
    IMemoryCache cache) =>
{
    var m = await catalog.UserCompanies.FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId);
    if (m is null) return Results.NotFound();
    catalog.UserCompanies.Remove(m);
    // La baja de la membresía cierra sus sesiones (sello nuevo).
    if (await catalog.Users.FindAsync(userId) is AppUser afectado) Sesiones.Rotar(afectado, cache);
    else Sesiones.Olvidar(cache, userId);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "membership-removed", Detail = $"{userId} @ {tenantId}", UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization(AdminPlataforma.Politica);

// Activar / desactivar una compañía.
app.MapPost("/admin/tenants/{id:guid}/status", async (Guid id, StatusRequest req, ITenantContext tc, CatalogDbContext catalog) =>
{
    var allowed = new[] { "active", "inactive" };
    if (!allowed.Contains(req.Status)) return Results.BadRequest("Estado inválido.");
    var tenant = await catalog.Tenants.FindAsync(id);
    if (tenant is null) return Results.NotFound();
    tenant.Status = req.Status;
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "tenant-status", Detail = $"{tenant.Name} -> {req.Status}", UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization(AdminPlataforma.Politica);

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
    if (!ContenidoAcceso.PuedeCrear(tc.Role)) return Results.Forbid();
    var nombre = (req.Name ?? "").Trim();
    if (nombre.Length is < 1 or > 200) return Results.BadRequest("El nombre de la categoría debe tener entre 1 y 200 caracteres.");
    var db = sp.GetRequiredService<TenantDbContext>();
    var cat = new Category { Name = nombre, ParentId = req.ParentId };
    db.Categories.Add(cat);
    await db.SaveChangesAsync();
    return Results.Ok(cat);
}).RequireAuthorization();

// Autores: todos los cursos con todos sus campos. Resto: con lo que las tarjetas del
// catálogo necesitan (título, descripción, categoría y portada), y exactamente de los
// mismos cursos que puede traer /catalog (CatalogLogic.ResolveAsync): no archivados y
// con alguna versión publicada. Así ninguna tarjeta del catálogo se queda sin su
// categoría ni su portada, y no se ve nada que el catálogo no ofrezca.
app.MapGet("/trainings", async (ITenantContext tc, IServiceProvider sp) =>
{
    if (tc.TenantId is null) return Results.BadRequest("No tenant context for this user.");
    var db = sp.GetRequiredService<TenantDbContext>();
    if (ContenidoAcceso.PuedeCrear(tc.Role))
        return Results.Ok(await db.Trainings.OrderByDescending(t => t.CreatedAt).ToListAsync());

    var publicados = await db.Trainings
        .Where(t => t.Status != "archived"
            && db.TrainingVersions.Any(v => v.TrainingId == t.Id && v.Status == "published"))
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new { t.Id, t.Title, t.Description, t.CategoryId, t.Status, t.PlayerConfigJson })
        .ToListAsync();
    return Results.Ok(publicados.Select(t => new
    {
        t.Id, t.Title, t.Description, t.CategoryId, t.Status,
        playerConfigJson = ContenidoAcceso.SoloPortada(t.PlayerConfigJson)
    }));
}).RequireAuthorization();

app.MapPost("/trainings", async (CreateTrainingRequest req, ITenantContext tc, IServiceProvider sp) =>
{
    if (tc.TenantId is null) return Results.BadRequest("No tenant context for this user.");
    if (!ContenidoAcceso.PuedeCrear(tc.Role)) return Results.Forbid();
    var titulo = (req.Title ?? "").Trim();
    if (titulo.Length is < 3 or > 300) return Results.BadRequest("El título debe tener entre 3 y 300 caracteres.");
    var db = sp.GetRequiredService<TenantDbContext>();
    var t = new Training
    {
        Title = titulo,
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
app.MapCompliance();

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

    // La política que aplica al entrar: la MÁS ESTRICTA entre todas las compañías del
    // usuario (required > optional > off). Así no se evita el doble factor entrando por
    // una compañía que lo tiene apagado y cambiando después a otra que lo exige. Sin
    // compañías activas (admin de plataforma) queda "optional".
    public static string PoliticaEfectiva(IEnumerable<Membresia> compañias)
    {
        var politicas = compañias.Select(c => string.IsNullOrWhiteSpace(c.Politica2FA) ? "optional" : c.Politica2FA).ToList();
        if (politicas.Count == 0) return "optional";
        if (politicas.Contains("required")) return "required";
        return politicas.All(p => p == "off") ? "off" : "optional";
    }
}

// Retos de segundo factor: alta, envío del código y consumo.
static class DosFactores
{
    public const int VigenciaMinutos = 10;
    public const int MaxIntentos = 5;

    // true solo en Development (lo fija Program al arrancar).
    public static bool RegistrarCodigosEnLog { get; set; }

    // destino/sesionHasta: reto abierto por /me/switch-company (ver TwoFactorChallenge).
    public static async Task<TwoFactorChallenge> AbrirRetoAsync(CatalogDbContext catalog, AppUser user,
        string proposito, IEmailSender email, ILoggerFactory logs, IConfiguration cfg, string? modoForzado = null,
        Guid? destino = null, DateTime? sesionHasta = null)
    {
        var modo = modoForzado ?? user.TwoFactorMode;
        var ahora = DateTime.UtcNow;

        // Un reto vivo a la vez por usuario. Abrir uno nuevo ya no regala intentos: los
        // fallos se suman en el usuario (AppUser.TwoFactorFailedCount).
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
            ExpiresAt = ahora.AddMinutes(VigenciaMinutos),
            TargetTenantId = destino,
            SessionExpiresAt = sesionHasta
        };
        catalog.TwoFactorChallenges.Add(reto);
        // Cada código enviado cuenta para el tope por cuenta (60 s entre envíos, 10 al día).
        if (modo == "email") EventosSeguridad.Anotar(catalog, EventosSeguridad.CodigoEnviado, user.Id);
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
            // El código solo va al log en desarrollo; fuera, solo que no se envió.
            if (string.IsNullOrWhiteSpace(cfg["Email:ApiKey"]))
            {
                if (RegistrarCodigosEnLog)
                    logs.CreateLogger("DosFactores").LogWarning(
                        "Email:ApiKey no está configurado: código de verificación para {Email}: {Codigo}", user.Email, codigo);
                else
                    logs.CreateLogger("DosFactores").LogError(
                        "Email:ApiKey no está configurado: no se envió el código de verificación a {Email}.", user.Email);
            }
        }
        return reto;
    }

    // Espera: con valor, la respuesta es 429 (doble factor bloqueado).
    public sealed record Resultado(bool Ok, AppUser? User, TwoFactorChallenge? Reto, string? Error, TimeSpan? Espera = null);

    // Canjea un código. El intento se consume ANTES de verificar, con un UPDATE
    // condicionado (Attempts < MaxIntentos): peticiones en paralelo no se saltan el tope
    // de 5 por reto. Cada fallo suma además al contador del usuario (todos los retos, TOTP
    // y códigos por correo): a los 5, el doble factor queda bloqueado 15 minutos. Un código
    // TOTP solo vale una vez (AppUser.LastTotpStep). dueño: el reto debe ser de ese usuario.
    public static async Task<Resultado> ConsumirAsync(CatalogDbContext catalog, Guid challengeId, string? codigo,
        string proposito, IEmailSender email, ILogger log, Guid? dueño = null)
    {
        const string noValido = "El código no es válido. Vuelve a iniciar sesión.";
        var ahora = DateTime.UtcNow;
        var reto = await catalog.TwoFactorChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == challengeId);
        if (reto is null || reto.UsedAt is not null || reto.Purpose != proposito || (dueño is Guid d && reto.UserId != d))
            return new(false, null, null, noValido);
        if (reto.ExpiresAt <= ahora)
            return new(false, null, null, "El código venció. Pide uno nuevo.");

        var user = await catalog.Users.FindAsync(reto.UserId);
        if (user is null) return new(false, null, null, noValido);
        if (Bloqueos.Espera(user.TwoFactorLockedUntil) is TimeSpan espera)
            return new(false, null, null, Demasiados.Mensaje(espera), espera);

        var n = await catalog.TwoFactorChallenges
            .Where(c => c.Id == reto.Id && c.UsedAt == null && c.ExpiresAt > ahora && c.Attempts < MaxIntentos)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Attempts, c => c.Attempts + 1));
        if (n == 0) return new(false, null, null, "Demasiados intentos. Vuelve a iniciar sesión.");

        bool valido;
        if (reto.Mode == "totp")
        {
            var paso = Totp.Verificar(user.TotpSecret, codigo);
            valido = paso is long p && p > (user.LastTotpStep ?? long.MinValue) && await MarcarPasoAsync(catalog, user.Id, p);
        }
        else
            valido = reto.CodeHash is not null && !string.IsNullOrWhiteSpace(codigo) &&
                     CryptographicOperations.FixedTimeEquals(
                         Encoding.UTF8.GetBytes(OtpCodigos.Hash(codigo!)), Encoding.UTF8.GetBytes(reto.CodeHash));

        if (!valido)
        {
            var hasta = await Bloqueos.Fallo2faAsync(catalog, user, email, log);
            if (hasta is DateTime h)
                return new(false, null, null, Demasiados.Mensaje(h - DateTime.UtcNow), h - DateTime.UtcNow);
            var quedan = Math.Min(MaxIntentos - (reto.Attempts + 1), await Bloqueos.Restantes2faAsync(catalog, user.Id));
            return new(false, null, null, quedan > 0
                ? $"Código incorrecto. Te quedan {quedan} intento(s)."
                : "Código incorrecto. Vuelve a iniciar sesión.");
        }

        // Un solo uso también en paralelo.
        n = await catalog.TwoFactorChallenges.Where(c => c.Id == reto.Id && c.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, (DateTime?)ahora));
        if (n == 0) return new(false, null, null, noValido);

        await Bloqueos.Limpiar2faAsync(catalog, user.Id);
        return new(true, user, reto, null);
    }

    // Guarda el paso TOTP aceptado solo si es posterior al último (UPDATE condicionado):
    // false = ese código (o uno anterior) ya se había usado.
    public static async Task<bool> MarcarPasoAsync(CatalogDbContext catalog, Guid userId, long paso)
        => await catalog.Users.Where(u => u.Id == userId && (u.LastTotpStep == null || u.LastTotpStep < paso))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastTotpStep, (long?)paso)) > 0;
}

static class Bootstrap
{
    public const string ClaveDeEjemplo = "ChangeMe123!";

    // Crea el admin de plataforma solo si el catálogo está vacío. Fuera de Development
    // no siembra con la contraseña de ejemplo ni sin contraseña (se registra el error y
    // la app sigue sin admin: se define Bootstrap:AdminEmail/AdminPassword por variable
    // de entorno y se reinicia). Siempre nace con cambio de contraseña obligatorio.
    public static async Task SeedAdminAsync(CatalogDbContext catalog, IConfiguration cfg, IHostEnvironment env, ILogger log)
    {
        if (!env.IsDevelopment() &&
            await catalog.Users.AnyAsync(u => u.Email == "admin@local" && u.TenantId == null && u.Role == "Admin"))
            log.LogError("Existe el admin de plataforma admin@local: cámbiale el correo por uno real y activa su doble factor.");

        if (await catalog.Users.AnyAsync()) return;

        var email = (cfg["Bootstrap:AdminEmail"] ?? "").Trim();
        var pass = cfg["Bootstrap:AdminPassword"] ?? "";
        if (!env.IsDevelopment() && (email.Length == 0 || pass.Length == 0 || pass == ClaveDeEjemplo))
        {
            log.LogError("No se creó el admin de plataforma: define Bootstrap:AdminEmail y Bootstrap:AdminPassword " +
                         "(APRENDOR_Bootstrap__AdminEmail / APRENDOR_Bootstrap__AdminPassword) con una contraseña propia.");
            return;
        }
        if (email.Length == 0) email = "admin@local";
        if (pass.Length == 0) pass = ClaveDeEjemplo;   // solo en desarrollo

        catalog.Users.Add(new AppUser
        {
            Email = email,
            Name = "Platform Admin",
            Role = "Admin",
            TenantId = null,
            MustChangePassword = true,
            PasswordHash = PasswordHasher.Hash(pass)
        });
        await catalog.SaveChangesAsync();
        Console.WriteLine($"Seeded platform admin: {email}");
    }
}

// Comprobaciones de arranque fuera de Development: si algo falta, la app no arranca y
// el mensaje dice qué variable definir.
static class ArranqueSeguro
{
    public static void Validar(IConfiguration cfg)
    {
        var key = cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Contains("CHANGE-ME", StringComparison.OrdinalIgnoreCase)
            || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException(
                "Jwt:Key no está configurada, es la de ejemplo o mide menos de 32 bytes. Define APRENDOR_Jwt__Key " +
                "con al menos 32 bytes aleatorios (por ejemplo, 64 caracteres base64) en las variables de entorno del servidor.");

        var baseUrl = cfg["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                "App:BaseUrl no está configurada o no es https. Define APRENDOR_App__BaseUrl con la URL pública " +
                "(p. ej. https://aprendor.midominio.com): los enlaces de los correos se arman solo con ella.");
    }

    // Lista de configuración: acepta un arreglo (Security:TrustedProxies:0, :1...) o un
    // valor único separado por comas o punto y coma (cómodo en una variable de entorno).
    public static List<string> Lista(IConfiguration cfg, string clave)
    {
        var s = cfg.GetSection(clave);
        var valores = s.GetChildren().Select(c => c.Value).ToList();
        if (!string.IsNullOrWhiteSpace(s.Value)) valores.Add(s.Value);
        return valores.Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
    }
}

// Límites de tamaño de petición por endpoint (el global es Security:MaxRequestBytes).
static class Limites
{
    // Subida de medios: Media:MaxBytes, 50 MB por defecto.
    public static long MediaMaxBytes(IConfiguration cfg) => cfg.GetValue<long?>("Media:MaxBytes") ?? 50L * 1024 * 1024;

    // Contenido del autor (ítems con imágenes embebidas, portada, logo y firma del
    // certificado): Security:AuthoringMaxRequestBytes, 8 MB por defecto.
    public static long AutorMaxBytes(IConfiguration cfg) => cfg.GetValue<long?>("Security:AuthoringMaxRequestBytes") ?? 8L * 1024 * 1024;
}

// Punto único de las políticas de autorización: la exigencia de scope=full (los tokens
// restringidos no sirven para la API) vale en DefaultPolicy, FallbackPolicy, "Admin" y
// "PlatformAdmin" a la vez.
static class PoliticasAcceso
{
    public const string Sesion = "Sesion";

    public static AuthorizationPolicyBuilder Base(AuthorizationPolicyBuilder p)
        => p.RequireAuthenticatedUser().RequireClaim("scope", Alcances.Full);

    public static AuthorizationPolicy Usuario() => Base(new AuthorizationPolicyBuilder()).Build();
}

// Cabeceras de seguridad de toda respuesta. La CSP no es estricta ('unsafe-inline'):
// el front usa un script en línea y manejadores onclick; permite Google Fonts,
// Turnstile de Cloudflare y los videos de YouTube que los cursos pueden incrustar.
// /media y el PDF de /c/{token} ponen además la suya, más estricta (sandbox).
// object-src 'none': probado el 25 sep 2026 en Chrome 153, Edge 153 y Firefox, los PDF
// se siguen viendo, tanto los servidos con esta CSP como los que el front abre como
// blob (que heredan la CSP de la página).
static class CabecerasSeguridad
{
    public const string Csp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com; " +
        "frame-src https://challenges.cloudflare.com https://www.youtube-nocookie.com https://www.youtube.com; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' blob:; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self'";

    public static void Aplicar(IHeaderDictionary h)
    {
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "SAMEORIGIN";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"] = Csp;
    }
}

// Alta de compañías con plantilla de conexión (Tenants:ConnectionTemplate con {db}).
static class PlantillaTenant
{
    public const string Prefijo = "TP_";
    public const string Patron = "^[A-Za-z0-9_]{3,50}$";
    private static readonly Regex Nombre = new(Patron, RegexOptions.Compiled);

    public static string? Leer(IConfiguration cfg)
    {
        var p = cfg["Tenants:ConnectionTemplate"];
        return !string.IsNullOrWhiteSpace(p) && p.Contains("{db}") ? p : null;
    }

    public static bool NombreValido(string? s) => s is not null && Nombre.IsMatch(s);

    // TP_ + el nombre de la compañía sin acentos, con solo letras, números y guion bajo.
    public static string NombrePorDefecto(string compañia)
    {
        var sinAcentos = new string(compañia.Normalize(NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        var slug = Regex.Replace(sinAcentos, "[^A-Za-z0-9]+", "_").Trim('_');
        var nombre = Prefijo + (slug.Length == 0 ? "Compania" : slug);
        return nombre.Length > 50 ? nombre[..50].TrimEnd('_') : nombre;
    }
}

// Roles con permiso para crear contenido en la compañía activa.
static class ContenidoAcceso
{
    public static bool PuedeCrear(string? rol) => rol is "Admin" or "Author" or "Moderator";

    // De la configuración del reproductor, solo la portada (lo que pinta la tarjeta del catálogo).
    public static string SoloPortada(string? json)
    {
        try
        {
            var cover = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json!)?["presentation"]?["cover"];
            var salida = new System.Text.Json.Nodes.JsonObject();
            if (cover is not null)
                salida["presentation"] = new System.Text.Json.Nodes.JsonObject { ["cover"] = cover.DeepClone() };
            return salida.ToJsonString();
        }
        catch { return "{}"; }
    }
}

// Qué usuarios puede gestionar quien llama a /admin/users*: el admin de plataforma,
// todos; el Admin de una compañía, solo los de su compañía activa (principal o por
// UserCompany) y nunca a un admin de plataforma.
static class AdminUsuarios
{
    public static readonly string[] Roles = { "Admin", "Author", "Moderator", "Learner" };

    // Compañia null = admin de plataforma (sin límite).
    public record Alcance(Guid? Compañia);

    public static async Task<Alcance?> AlcanceAsync(ITenantContext tc, ClaimsPrincipal principal,
        CatalogDbContext catalog, IMemoryCache cache)
    {
        if (tc.TenantId is Guid tid) return tc.Role == "Admin" ? new Alcance(tid) : null;
        return await AdminPlataforma.EsAsync(principal, catalog, cache) ? new Alcance(null) : null;
    }

    public static async Task<bool> PuedeGestionarAsync(Alcance alcance, AppUser u, CatalogDbContext catalog)
    {
        if (alcance.Compañia is not Guid tid) return true;
        if (u.TenantId is null && u.Role == "Admin") return false;   // admin de plataforma
        return await Membresias.EsMiembroAsync(catalog, tid, u.Id);
    }

    // ¿La persona pertenece a alguna compañía distinta de "suya" (principal o UserCompany)?
    // Lo que afecta a la cuenta entera (clave, baja) no lo decide el Admin de una sola.
    public static async Task<bool> TieneOtraCompañiaAsync(AppUser u, Guid suya, CatalogDbContext catalog)
    {
        if (u.TenantId is Guid principal && principal != suya) return true;
        return await catalog.UserCompanies.AnyAsync(m => m.UserId == u.Id && m.TenantId != suya);
    }
}

// ---- Request DTOs ----
record LoginRequest(string? Email, string? Password);
record CreateTenantRequest(string Name, string? ConnectionString = null, string? DatabaseName = null);
record CreateUserRequest(string Email, string Name, string? Password, string Role, Guid? TenantId, bool? SendInvite);
record CreateCategoryRequest(string Name, Guid? ParentId);
record CreateTrainingRequest(string Title, string? Description, Guid? CategoryId);
record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
record RoleRequest(string Role);
record ResetPasswordRequest(string? TempPassword);
record ForgotPasswordRequest(string? Email);
record ResetWithTokenRequest(string? Token, string? NewPassword);
record TwoFactorVerifyRequest(Guid ChallengeId, string? Code);
record ResendRequest(Guid ChallengeId);
record TwoFactorSetupRequest(string? Mode, string? CurrentPassword = null, string? Code = null);
record TwoFactorConfirmRequest(string? Mode, string? Code, Guid? ChallengeId);
record SwitchCompanyRequest(Guid TenantId);
record TwoFactorPolicyRequest(string Policy);
record CompanyMembershipRequest(Guid UserId, Guid TenantId, string Role);
record ComplianceOfficerRequest(bool IsOfficer, Guid? TenantId = null);
record DisableTwoFactorRequest(string? CurrentPassword);
record StatusRequest(string Status);
