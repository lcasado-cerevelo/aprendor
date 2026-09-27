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

// Consola: "voz-texto <course.json>" muestra lo que leerá la voz en cada lámina de un curso
// de content/ y cuántos caracteres gasta (Azure cobra por carácter: 500 mil gratis al mes).
// No toca la base ni Azure.
if (args.Length > 1 && args[0].Equals("voz-texto", StringComparison.OrdinalIgnoreCase))
{
    TrainingPlatform.Narracion.Consola(args[1]);
    return;
}
// Consola: "certificado-muestra <salida.pdf>" genera un certificado de ejemplo (para revisar
// el diseño o cómo imprime). No toca la base.
if (args.Length > 1 && args[0].Equals("certificado-muestra", StringComparison.OrdinalIgnoreCase))
{
    var hoy = DateTime.UtcNow;
    File.WriteAllBytes(args[1], TrainingPlatform.Certificates.CertificatePdf.Render("Advance Logistics", "Nombre Apellido de Ejemplo",
        "Hostigamiento Sexual en el Empleo", "CERT-2026-MUESTRA", hoy, hoy.AddYears(1), 100, 70, true, true,
        "Certifica haber completado el adiestramiento sobre Hostigamiento Sexual en el Empleo bajo la Ley Núm. 17 de Puerto Rico.",
        "Recursos Humanos", "Advance Logistics", "#a21caf"));
    Console.WriteLine($"Certificado de muestra escrito en {args[1]}");
    return;
}

var builder = WebApplication.CreateBuilder(args);
// Además de las variables de entorno sin prefijo que ya carga CreateBuilder, admite
// las mismas claves con el prefijo APRENDOR_ (p. ej. APRENDOR_Email__ApiKey), para
// no chocar con otras variables de entorno en una máquina que corre varios proyectos.
builder.Configuration.AddEnvironmentVariables(prefix: "APRENDOR_");
var cfg = builder.Configuration;
var esDesarrollo = builder.Environment.IsDevelopment();
var modoMigracion = args.Length > 0 && args[0].Equals("migrate", StringComparison.OrdinalIgnoreCase);
// Consola: "reset-2fa <correo>" quita el doble factor de una cuenta. Es la salida para el
// admin de plataforma que pierde el autenticador (a él no se le recupera por correo).
var modoReset2fa = args.Length > 0 && args[0].Equals("reset-2fa", StringComparison.OrdinalIgnoreCase);
var modoConsola = modoMigracion || modoReset2fa;

// Arranque seguro: fuera de Development la app NO arranca con la clave JWT de ejemplo
// ni sin la URL pública (los enlaces de los correos se arman solo con ella). Los modos
// de consola solo tocan la base, así que no lo exigen.
if (!esDesarrollo && !modoConsola) ArranqueSeguro.Validar(cfg);

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
    // Cada entrada es una IP (10.0.0.5) o un rango CIDR (173.245.48.0/20). Los rangos
    // sirven para poner el proxy de Cloudflare (nube naranja) delante de IIS: sus IPs
    // de borde se publican como rangos, no como direcciones sueltas.
    foreach (var p in ArranqueSeguro.Lista(cfg, "Security:TrustedProxies"))
    {
        var partes = p.Split('/', 2);
        if (partes.Length == 2 && IPAddress.TryParse(partes[0], out var red) && int.TryParse(partes[1], out var prefijo)
            && prefijo >= 0 && prefijo <= (red.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32))
            o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(red, prefijo));
        else if (IPAddress.TryParse(p, out var ip))
            o.KnownProxies.Add(ip);
    }
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

// HSTS (solo fuera de Development). La redirección de http a https es opcional
// (Security:RedirectHttps): con IIS publicado directo a internet se enciende; detrás de
// un túnel el HTTPS lo fuerza el borde (Cloudflare) y se deja apagada.
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
// Cloudflare Turnstile en el acceso (bloque S3): validación en el servidor con 5 s de espera máxima.
builder.Services.AddHttpClient(Turnstile.ClienteHttp, c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<Turnstile>();
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
// Redes de confianza de la instancia (Security:TrustedNetworks) y aviso si Turnstile
// está encendido sin la clave secreta (entonces queda apagado).
{
    var logSeguridad = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Seguridad");
    RedesConfianza.Configurar(cfg, logSeguridad);
    if (!modoConsola) app.Services.GetRequiredService<Turnstile>().AdvertirAlArrancar();
}
// Zona horaria de la aplicación (App:TimeZone, por defecto America/Puerto_Rico): con ella
// se muestran las fechas y se cuentan los días, esté el servidor en la zona que esté.
if (HoraLocal.Configurar(cfg["App:TimeZone"]) is string avisoZona)
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HoraLocal").LogWarning("{Aviso}", avisoZona);

// ---- CLI mode: apply migrations to the catalog + every tenant database ----
if (modoMigracion)
{
    await MigrationRunner.RunAsync(app.Services);
    return;
}

// dotnet TrainingPlatform.dll reset-2fa correo@dominio
// Quita la app autenticadora de esa cuenta y cierra todas sus sesiones; al volver a entrar,
// si la cuenta lo exige (administradores o compañías con política obligatoria), tendrá que
// registrar el autenticador de nuevo. Queda en la auditoría del catálogo. Solo lo puede
// correr quien tiene acceso al servidor y a la base: es el último recurso.
if (modoReset2fa)
{
    // El correo es el primer argumento con arroba (se ignoran opciones como --contentRoot).
    var correo = args.Skip(1).Select(a => a.Trim()).FirstOrDefault(a => !a.StartsWith("--") && a.Contains('@')) ?? "";
    if (correo.Length == 0) { Console.WriteLine("Uso: dotnet TrainingPlatform.dll reset-2fa correo@dominio"); Environment.ExitCode = 2; return; }
    using var scopeConsola = app.Services.CreateScope();
    var catalogo = scopeConsola.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var cuenta = await catalogo.Users.FirstOrDefaultAsync(u => u.Email == correo);
    if (cuenta is null) { Console.WriteLine($"No existe ninguna cuenta con el correo {correo}."); Environment.ExitCode = 1; return; }
    DobleFactor.Quitar(catalogo, cuenta, scopeConsola.ServiceProvider.GetRequiredService<IMemoryCache>());
    catalogo.AuditLogs.Add(new CatalogAuditLog
    {
        Action = "2fa-reset-console",
        Detail = $"{cuenta.Email} (desde la consola del servidor, usuario de Windows {Environment.UserName})",
        UserId = cuenta.Id
    });
    await catalogo.SaveChangesAsync();
    Console.WriteLine($"Listo: se quitó el doble factor de {cuenta.Email} y se cerraron sus sesiones. Al entrar tendrá que registrar su app autenticadora de nuevo.");
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

// http -> https (Security:RedirectHttps). No se hace con UseHttpsRedirection ni con
// URL Rewrite en web.config (cada publicación lo sobrescribe): así se deja pasar lo que
// entra por localhost, que es cómo se corren los seeds y el migrador en el servidor
// (http://localhost:8086). 308 conserva el método y el cuerpo.
if (cfg.GetValue<bool>("Security:RedirectHttps"))
{
    var puertoHttps = cfg.GetValue<int?>("Security:HttpsPort");
    app.Use(async (ctx, next) =>
    {
        var host = ctx.Request.Host.Host;
        var esLocal = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                      || (IPAddress.TryParse(host, out var ipHost) && IPAddress.IsLoopback(ipHost));
        if (ctx.Request.IsHttps || esLocal || string.IsNullOrEmpty(host)) { await next(); return; }

        var destino = new UriBuilder("https", host, puertoHttps is int p && p != 443 ? p : -1).Uri.GetLeftPart(UriPartial.Authority)
                      + ctx.Request.PathBase + ctx.Request.Path + ctx.Request.QueryString;
        ctx.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
        ctx.Response.Headers.Location = destino;
    });
}

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
// Las páginas (index.html, player.html, certificate.html) llevan todo el front en línea: sin
// Cache-Control el navegador las puede guardar por heurística (horas o días según su
// Last-Modified) y, tras publicar, seguir corriendo la versión anterior contra la API nueva.
// no-cache: se pueden guardar, pero se revalidan en cada carga (ETag, 304 si no cambió).
// Es higiene de despliegue, no la causa del «Validar correo» que no avanzaba y daba 400 en el
// segundo clic ni de la pantalla de entrada del curso que no salía: eso era el front del
// 24 sep, que seguía publicado en el servidor (ver «Comprobar la publicación» en README.md).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

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
// Turnstile se valida antes de buscar el usuario. Desde una red de confianza (de la
// instancia o de las compañías de la persona) no se pide el doble factor ni su alta: el
// token lleva amr=mfa-trusted y queda auditado (2fa-skipped-trusted).
app.MapPost("/auth/login", async (LoginRequest? req, HttpContext http, Turnstile turnstile, CatalogDbContext catalog,
    JwtTokenService jwt, IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var verificacion = await turnstile.ValidarAsync(http, req?.TurnstileToken);
    if (verificacion != Turnstile.Resultado.Ok) return Turnstile.Respuesta(verificacion);

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

    // El intento se reserva ANTES de comprobar la clave (UPDATE condicionado): con
    // peticiones en paralelo no se comprueban más de 5 claves entre bloqueo y bloqueo.
    var logLogin = logs.CreateLogger("Login");
    if (!await Bloqueos.ReservarLoginAsync(catalog, user.Id))
    {
        PasswordHasher.VerifyFicticio(clave);
        return Demasiados.Resultado(await Bloqueos.EsperaLoginAsync(catalog, user, email, logLogin));
    }

    if (!PasswordHasher.Verify(clave, user.PasswordHash))
    {
        var hasta = await Bloqueos.FalloLoginAsync(catalog, user, email, logLogin);
        return hasta is DateTime h ? Demasiados.Resultado(h - DateTime.UtcNow) : Results.Unauthorized();
    }
    await Bloqueos.LimpiarLoginAsync(catalog, user.Id);

    // Cuenta desactivada: se dice solo después de comprobar la contraseña (así no sirve
    // para averiguar qué correos existen).
    if (user.DeactivatedAt is not null)
        return Results.Json(new { error = "Esta cuenta está desactivada. Si crees que es un error, habla con el administrador de tu compañía." },
            statusCode: StatusCodes.Status403Forbidden);

    // La contraseña temporal que generó el admin vence a las 72 h.
    if (user.MustChangePassword && user.TempPasswordExpiresAt is DateTime vence && vence <= DateTime.UtcNow)
        return Results.Json(new { error = "La contraseña temporal venció. Usa «¿Olvidaste tu contraseña?» o pide al administrador una nueva." },
            statusCode: StatusCodes.Status401Unauthorized);

    var compañias = await Compañias.DeUsuarioAsync(catalog, user);
    var politica = Compañias.PoliticaParaUsuario(user, compañias);   // administradores: siempre required
    var tiene2fa = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;

    // Red de confianza: ni código ni alta del doble factor, aunque la política lo exija.
    var ip = ClientIp.Of(http);
    if (await RedesConfianza.ParaUsuarioAsync(catalog, user, ip))
    {
        if ((politica != "off" && tiene2fa) || (politica == "required" && !tiene2fa))
            catalog.AuditLogs.Add(new CatalogAuditLog
            {
                Action = "2fa-skipped-trusted",
                Detail = $"{user.Email} ({(tiene2fa ? "sin código" : "sin alta obligatoria")}, {ip})",
                UserId = user.Id
            });
        await EventosSeguridad.RegistrarAsync(catalog, EventosSeguridad.LoginOk, user.Id, user.TenantId);
        return Results.Ok(await RespuestaSesion.CrearAsync(catalog, jwt, user, Amr.MfaTrusted));
    }

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
// reto y responde requires2fa: el token llega al verificarlo en /auth/2fa/verify. Una
// sesión abierta desde una red de confianza (amr=mfa-trusted) solo vale como doble factor
// si la petición sigue llegando desde una red de confianza de la compañía destino.
app.MapPost("/me/switch-company", async (SwitchCompanyRequest req, HttpContext http, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();

    var compañias = await Compañias.DeUsuarioAsync(catalog, user);
    var destino = compañias.FirstOrDefault(c => c.TenantId == req.TenantId);
    if (destino is null) return Results.BadRequest("No perteneces a esa compañía.");

    var expira = RespuestaSesion.ExpiracionDe(principal);
    var amr = principal.FindFirst("amr")?.Value ?? Amr.Pwd;
    // Ser Admin en la compañía destino exige el doble factor igual que una política required,
    // y a un administrador no le vale la red de confianza.
    var adminDestino = destino.Rol == "Admin";
    var confianzaDestino = !adminDestino && amr == Amr.MfaTrusted
        && await RedesConfianza.ParaCompañiaAsync(catalog, destino.TenantId, ClientIp.Of(http));
    if ((destino.Politica2FA == "required" || adminDestino) && !confianzaDestino)
    {
        if (!(user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null))
            return Results.BadRequest($"{destino.Nombre} exige verificación en dos pasos. Actívala en tu perfil antes de entrar.");
        if (amr != Amr.Mfa)
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
    // El código de «Perdí mi autenticador» se trata como en /auth/2fa/recover/start: se
    // vuelve a comprobar que la cuenta pueda recuperarse (la compañía pudo apagarlo
    // entretanto), no se manda nada con el doble factor bloqueado (no se podría canjear) y
    // tiene su tope (3 al día). La respuesta es siempre la misma, con un challengeId: el
    // nuevo si se envió; si no, el mismo reto, que sigue vigente.
    if (viejo.Purpose == Recuperacion.Proposito)
    {
        IResult Recuperar(Guid id) => Results.Ok(new
        {
            challengeId = id,
            message = "Si tu cuenta permite recuperar el acceso por correo, te reenviamos el código. Revisa tu bandeja de entrada."
        });
        if (await Recuperacion.MotivoRechazoAsync(catalog, user) is not null) return Recuperar(viejo.Id);
        if (Bloqueos.Espera(user.TwoFactorLockedUntil) is not null) return Recuperar(viejo.Id);
        if (await Recuperacion.ReservarEnvioAsync(catalog, user.Id) is not null) return Recuperar(viejo.Id);
        var otro = await DosFactores.AbrirRetoAsync(catalog, user, viejo.Purpose, email, logs, cfg, modoForzado: "email",
            destino: viejo.TargetTenantId, sesionHasta: viejo.SessionExpiresAt, envioContado: true);
        catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-recover-code-sent", Detail = $"{user.Email} (reenvío)", UserId = user.Id });
        await catalog.SaveChangesAsync();
        return Recuperar(otro.Id);
    }

    if (await Bloqueos.ReservarEnvioCodigoAsync(catalog, user.Id) is TimeSpan e) return Demasiados.Resultado(e);
    var nuevo = await DosFactores.AbrirRetoAsync(catalog, user, viejo.Purpose, email, logs, cfg, modoForzado: "email",
        destino: viejo.TargetTenantId, sesionHasta: viejo.SessionExpiresAt, envioContado: true);
    return Results.Ok(new { challengeId = nuevo.Id, message = "Te reenviamos el código." });
}).AllowAnonymous();

// Lo que la pantalla de acceso necesita saber antes de entrar: la site key de Turnstile
// (null si está apagado, sin claves o si la petición viene de una red de confianza de
// la instancia; entonces no se carga el widget).
app.MapGet("/auth/config", (HttpContext http, Turnstile turnstile) =>
    Results.Ok(new { turnstileSiteKey = turnstile.SiteKeyPara(http) }))
    .AllowAnonymous();

// El icono de la pestaña (/favicon.ico, /favicon.svg, /img/apple-touch-icon.png) está en
// wwwroot y lo sirve UseStaticFiles antes de la autorización, así que no le aplica la
// FallbackPolicy. No mapear aquí /favicon.ico: un endpoint con esa ruta haría que
// UseStaticFiles se saltara el archivo. Se regenera con tools/Generar-Favicon.ps1.

// ---------- «Perdí mi autenticador» ----------
// Paso 1, desde la pantalla del código (reto de login vigente, así que ya dio la clave
// buena): si el correo está validado, sus compañías no apagaron la recuperación y no es
// admin de plataforma, se envía un código de 6 dígitos (10 min, 5 intentos) al correo.
// La respuesta es SIEMPRE la misma, con un challengeId (inventado si no se envió nada).
// Límites: 5/h por IP y 3 al día por cuenta.
app.MapPost("/auth/2fa/recover/start", async (RecoverStartRequest? req, HttpContext http, Turnstile turnstile,
    CatalogDbContext catalog, IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var verificacion = await turnstile.ValidarAsync(http, req?.TurnstileToken);
    if (verificacion != Turnstile.Resultado.Ok) return Turnstile.Respuesta(verificacion);

    IResult Generico(Guid id) => Results.Ok(new
    {
        challengeId = id,
        message = "Si tu cuenta permite recuperar el acceso por correo, te enviamos un código. Revisa tu bandeja de entrada."
    });

    var ahora = DateTime.UtcNow;
    var reto = req is null ? null
        : await catalog.TwoFactorChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ChallengeId);
    if (reto is null || reto.UsedAt is not null || reto.ExpiresAt <= ahora || reto.Purpose != "login"
        || reto.Mode != "totp" || reto.TargetTenantId is not null)
        return Generico(Guid.NewGuid());
    var user = await catalog.Users.FindAsync(reto.UserId);
    if (user is null) return Generico(Guid.NewGuid());

    if (await Recuperacion.MotivoRechazoAsync(catalog, user) is string motivo)
    {
        catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-recover-denied", Detail = $"{user.Email}: {motivo}", UserId = user.Id });
        await catalog.SaveChangesAsync();
        return Generico(Guid.NewGuid());
    }
    // Con el doble factor bloqueado el código no se podría canjear: no se manda.
    if (Bloqueos.Espera(user.TwoFactorLockedUntil) is not null) return Generico(Guid.NewGuid());
    if (await Recuperacion.ReservarEnvioAsync(catalog, user.Id) is not null) return Generico(Guid.NewGuid());

    var nuevo = await DosFactores.AbrirRetoAsync(catalog, user, Recuperacion.Proposito, email, logs, cfg,
        modoForzado: "email", envioContado: true);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-recover-code-sent", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    return Generico(nuevo.Id);
}).AllowAnonymous();

// Paso 2: canjear el código (cuenta para el bloqueo del doble factor). Quita el
// autenticador, cierra las demás sesiones (sello nuevo), avisa a la persona y, si la
// compañía lo pide, a sus Admin. Devuelve la sesión como el login: con alcance enroll-2fa
// si la política exige el doble factor y no entra desde una red de confianza.
app.MapPost("/auth/2fa/recover/verify", async (TwoFactorVerifyRequest req, HttpContext http, CatalogDbContext catalog,
    JwtTokenService jwt, IMemoryCache cache, IEmailSender email, ILoggerFactory logs) =>
{
    var log = logs.CreateLogger("DosFactores");
    var r = await DosFactores.ConsumirAsync(catalog, req.ChallengeId, req.Code, Recuperacion.Proposito, email, log);
    if (r.Espera is TimeSpan espera) return Demasiados.Resultado(espera, r.Error);
    if (!r.Ok || r.User is null) return Results.BadRequest(r.Error ?? "El código no es válido.");
    var user = r.User;

    // Pudo cambiar en los 10 minutos del código (la compañía apagó la recuperación...).
    if (await Recuperacion.MotivoRechazoAsync(catalog, user) is string motivo)
    {
        catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-recover-denied", Detail = $"{user.Email}: {motivo}", UserId = user.Id });
        await catalog.SaveChangesAsync();
        return Results.BadRequest("No se puede recuperar el acceso de esta cuenta por correo. Pide ayuda al administrador de tu compañía.");
    }

    var ip = ClientIp.Of(http);
    var ahora = DateTime.UtcNow;
    DobleFactor.Quitar(catalog, user, cache);
    user.EmailVerifiedAt ??= ahora;          // acaba de demostrar que el correo es suyo
    PruebaCorreo.Anotar(catalog, user.Id, ip);
    EventosSeguridad.Anotar(catalog, EventosSeguridad.LoginOk, user.Id, user.TenantId);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-recovered", Detail = $"{user.Email} ({ip})", UserId = user.Id });
    await catalog.SaveChangesAsync();
    await Bloqueos.Limpiar2faAsync(catalog, user.Id);

    try
    {
        await email.SendAsync(user.Email, user.Name, "Quitamos la app autenticadora de tu cuenta de Aprendor",
            EmailTemplates.AuthenticatorReset(user.Name, "recovered", ahora));
    }
    catch (Exception ex) { log.LogWarning(ex, "No se pudo avisar de la recuperación del 2FA a {Email}", user.Email); }
    await AvisosSeguridad.AdminsAsync(catalog, email, log, user, "2fa-recovered", ip);

    var confia = await RedesConfianza.ParaUsuarioAsync(catalog, user, ip);
    return Results.Ok(await RespuestaSesion.CrearAsync(catalog, jwt, user, confia ? Amr.MfaTrusted : Amr.Pwd));
}).AllowAnonymous();

// ---------- Validación del correo del usuario ----------
// Se pide la primera vez que entra (token restringido verify-email). Reusa el mecanismo
// de retos con código del doble factor, con propósito distinto. Al validarlo se devuelve
// un token nuevo con el alcance que corresponda.
// Si el correo ya estaba validado (por ejemplo, con un enlace de restablecimiento en otra
// pestaña) también se devuelve un token con el alcance al día, pero sin prueba nueva
// RenovarAsync no alarga la sesión: vence cuando vencía el actual, así que llamar aquí
// una y otra vez no sirve para mantener viva una sesión robada.
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
    if (await Bloqueos.ReservarEnvioCodigoAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);

    var reto = await DosFactores.AbrirRetoAsync(catalog, user, "verify-email", email, logs, cfg, modoForzado: "email",
        envioContado: true);
    return Results.Ok(new { verified = false, challengeId = reto.Id, email = user.Email });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// Un segundo envío del mismo código (doble clic, Enter y clic, o una pantalla vieja que no
// avanzó) no da error: si el reto ya se canjeó porque el correo quedó validado, se responde
// igual que al primero, con la sesión al día.
app.MapPost("/me/email/verify", async (EmailVerifyRequest? req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, JwtTokenService jwt, IEmailSender email, ILoggerFactory logs) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (user.EmailVerifiedAt is null)
    {
        if (req?.ChallengeId is not Guid reto)
            return Results.BadRequest("No hay un código vigente en esta pantalla. Pulsa «Reenviar código» y escribe el nuevo.");
        var r = await DosFactores.ConsumirAsync(catalog, reto, req.Code, "verify-email", email,
            logs.CreateLogger("DosFactores"), dueño: user.Id);
        if (!r.Ok)
        {
            // Otra petición con el mismo código pudo validarlo mientras tanto: se mira la
            // base, no la copia leída al empezar. Si el reto de este usuario ya aparece
            // canjeado, la otra acertó el código aunque aún no haya guardado la fecha.
            var validado = await catalog.Users.AsNoTracking().Where(u => u.Id == user.Id)
                .Select(u => u.EmailVerifiedAt).FirstOrDefaultAsync();
            var hace = DateTime.UtcNow.AddMinutes(-DosFactores.VigenciaMinutos);
            var canjeado = validado is null && await catalog.TwoFactorChallenges.AsNoTracking().AnyAsync(c =>
                c.Id == reto && c.UserId == user.Id && c.Purpose == "verify-email" && c.UsedAt != null && c.UsedAt > hace);
            if (validado is null && !canjeado)
            {
                if (r.Espera is TimeSpan espera) return Demasiados.Resultado(espera, r.Error);
                return Results.BadRequest(r.Error ?? "El código no es válido.");
            }
            user.EmailVerifiedAt = validado ?? DateTime.UtcNow;
            if (validado is null) await catalog.SaveChangesAsync();
        }
        else
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            catalog.AuditLogs.Add(new CatalogAuditLog { Action = "email-verified", Detail = user.Email, UserId = user.Id });
            await catalog.SaveChangesAsync();
        }
    }
    // Con el correo ya validado no se comprueba ningún código: el token nuevo solo pone el
    // alcance al día y vence cuando vencía el actual (RenovarAsync no alarga sin prueba).
    // Solo estrena vida si el token era restringido y el paso quedó completado.
    var s = await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal);
    return Results.Ok(new { verified = true, s.Token, s.Scope, s.User });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// ---------- Alta, cambio y baja del segundo factor (usuario autenticado) ----------
// emailCodeRequired: la primera alta del autenticador desde fuera de las redes de
// confianza pide antes un código por correo (POST /me/2fa/setup/send-code).
// trustedNetworks: hay redes de confianza que le valen (la antesala del alta dice entonces
// «al entrar desde fuera de la oficina» en vez de «cada vez que entres»).
app.MapGet("/me/2fa", async (ITenantContext tc, HttpContext http, CatalogDbContext catalog) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    return Results.Ok(new { mode = user.TwoFactorMode, confirmed = user.TwoFactorConfirmedAt is not null,
                            pending = user.PendingTotpSecret is not null,
                            emailCodeRequired = await PruebaCorreo.HaceFaltaAsync(catalog, user, ClientIp.Of(http)),
                            trustedNetworks = await RedesConfianza.HayParaUsuarioAsync(catalog, user) });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// Código por correo para la primera alta del autenticador desde fuera de las redes de
// confianza: quien solo tiene la contraseña no puede registrar su propia app en la cuenta
// de otro. { required: false } si no hace falta. Por cuenta: 60 s entre envíos y 10 al día.
app.MapPost("/me/2fa/setup/send-code", async (ITenantContext tc, HttpContext http, CatalogDbContext catalog,
    IEmailSender email, ILoggerFactory logs, IConfiguration cfg) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    if (!await PruebaCorreo.HaceFaltaAsync(catalog, user, ClientIp.Of(http)))
        return Results.Ok(new { required = false });
    if (await Bloqueos.ReservarEnvioCodigoAsync(catalog, user.Id) is TimeSpan espera) return Demasiados.Resultado(espera);

    var reto = await DosFactores.AbrirRetoAsync(catalog, user, PruebaCorreo.Proposito, email, logs, cfg,
        modoForzado: "email", envioContado: true);
    return Results.Ok(new { required = true, challengeId = reto.Id, email = user.Email });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// Paso 1 del alta (o del cambio de autenticador): devuelve un secreto nuevo para
// escribirlo o escanearlo en la app. Queda PENDIENTE (PendingTotpSecret): el vigente sigue
// valiendo hasta confirmar. Si ya hay 2FA confirmado, cambiarlo exige la contraseña actual
// y un código válido del autenticador actual (quien solo tiene la sesión abierta no puede
// registrar el suyo). El segundo factor SOLO se hace con app: el correo no cuenta como
// segundo factor porque suele estar en el mismo dispositivo y con la misma sesión.
// La PRIMERA alta desde fuera de las redes de confianza exige además el código enviado al
// correo (emailChallengeId + emailCode, de /me/2fa/setup/send-code); sin él responde 400
// { requiresEmailCode: true }. Canjearlo vale 15 minutos desde la misma IP.
app.MapPost("/me/2fa/setup", async (TwoFactorSetupRequest? req, ITenantContext tc, HttpContext http,
    CatalogDbContext catalog, IEmailSender email, ILoggerFactory logs) =>
{
    var user = await catalog.Users.FindAsync(tc.UserId);
    if (user is null) return Results.NotFound();
    var modo = (req?.Mode ?? "totp").Trim().ToLowerInvariant();
    if (modo != "totp")
        return Results.BadRequest("La verificación en dos pasos se hace con app autenticadora.");

    var confirmado = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;
    var ip = ClientIp.Of(http);
    if (await PruebaCorreo.HaceFaltaAsync(catalog, user, ip))
    {
        if (req?.EmailChallengeId is not Guid retoCorreo || string.IsNullOrWhiteSpace(req.EmailCode))
            return Results.BadRequest(new
            {
                requiresEmailCode = true,
                error = "Antes de registrar la app autenticadora, confirma el código que te enviamos por correo."
            });
        var rc = await DosFactores.ConsumirAsync(catalog, retoCorreo, req.EmailCode, PruebaCorreo.Proposito, email,
            logs.CreateLogger("DosFactores"), dueño: user.Id);
        if (rc.Espera is TimeSpan esperaCorreo) return Demasiados.Resultado(esperaCorreo, rc.Error);
        if (!rc.Ok) return Results.BadRequest(new { requiresEmailCode = true, error = rc.Error ?? "El código no es válido." });
        // El código llegó al correo de la cuenta: también lo valida.
        if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            catalog.AuditLogs.Add(new CatalogAuditLog { Action = "email-verified", Detail = user.Email, UserId = user.Id });
        }
        PruebaCorreo.Anotar(catalog, user.Id, ip);
        await catalog.SaveChangesAsync();
    }

    if (confirmado)
    {
        var reserva = await Bloqueos.ReservarSensibleAsync(catalog, user.Id);
        if (reserva.Espera is TimeSpan espera) return Demasiados.Resultado(espera);
        if (!PasswordHasher.Verify(req?.CurrentPassword, user.PasswordHash))
        {
            if (await Bloqueos.FalloSensibleAsync(catalog, user.Id) is TimeSpan tope) return Demasiados.Resultado(tope);
            return Results.BadRequest("La contraseña actual no es correcta.");
        }
        Bloqueos.OkSensible(reserva);
        await catalog.SaveChangesAsync();

        // El código del autenticador vigente cuenta para el bloqueo del 2FA, reservado
        // antes de verificarlo como en /auth/2fa/verify.
        var log2fa = logs.CreateLogger("DosFactores");
        if (!await Bloqueos.Reservar2faAsync(catalog, user.Id))
            return Demasiados.Resultado(await Bloqueos.Espera2faAsync(catalog, user, email, log2fa));
        var paso = Totp.Verificar(user.TotpSecret, req?.Code);
        if (paso is not long p || p <= (user.LastTotpStep ?? long.MinValue) || !await DosFactores.MarcarPasoAsync(catalog, user.Id, p))
        {
            var hasta = await Bloqueos.Fallo2faAsync(catalog, user, email, log2fa);
            if (hasta is DateTime h) return Demasiados.Resultado(h - DateTime.UtcNow);
            return Results.BadRequest("El código de tu app autenticadora actual no es válido.");
        }
        await Bloqueos.Limpiar2faAsync(catalog, user.Id);
    }

    user.PendingTotpSecret = Totp.NuevoSecreto();   // aún no queda activo: falta confirmar
    await catalog.SaveChangesAsync();
    // uri y otpauthUri son el mismo URI (uri se mantiene por compatibilidad); qrDataUri es
    // su código QR en SVG, lo que la pantalla muestra primero para escanearlo.
    var otpauth = Totp.UriDeConfiguracion("Aprendor", user.Email, user.PendingTotpSecret);
    return Results.Ok(new
    {
        mode = "totp",
        secret = user.PendingTotpSecret,
        uri = otpauth,
        otpauthUri = otpauth,
        qrDataUri = Totp.QrDataUri(otpauth),
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

    var reserva = await Bloqueos.ReservarSensibleAsync(catalog, user.Id);
    if (reserva.Espera is TimeSpan espera) return Demasiados.Resultado(espera);
    var paso = Totp.Verificar(secreto, req?.Code);
    if (paso is null)
    {
        if (await Bloqueos.FalloSensibleAsync(catalog, user.Id) is TimeSpan tope) return Demasiados.Resultado(tope);
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
    Sesiones.Rotar(catalog, user, cache);
    Bloqueos.OkSensible(reserva);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = cambio ? "2fa-changed" : "2fa-enabled", Detail = $"{user.Email} (totp)", UserId = user.Id });
    await catalog.SaveChangesAsync();
    // Si el contador ya valía 0 al leer la fila, EF no lo escribe: un fallo contado
    // mientras tanto con ExecuteUpdate quedaría vivo. Se limpia en la base.
    await Bloqueos.Limpiar2faAsync(catalog, user.Id);

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
    var reserva = await Bloqueos.ReservarSensibleAsync(catalog, user.Id);
    if (reserva.Espera is TimeSpan espera) return Demasiados.Resultado(espera);
    if (!PasswordHasher.Verify(req?.CurrentPassword, user.PasswordHash))
    {
        if (await Bloqueos.FalloSensibleAsync(catalog, user.Id) is TimeSpan tope) return Demasiados.Resultado(tope);
        return Results.BadRequest("La contraseña actual no es correcta.");
    }
    Bloqueos.OkSensible(reserva);

    // Si alguna de sus compañías lo exige, o si es administrador, no puede quitárselo.
    var suyas = await Compañias.DeUsuarioAsync(catalog, user);
    if (Compañias.EsAdministrador(user, suyas))
    {
        await catalog.SaveChangesAsync();
        return Results.BadRequest("No se puede desactivar: los administradores deben tener verificación en dos pasos.");
    }
    var exigen = suyas.Where(c => c.Politica2FA == "required").Select(c => c.Nombre).ToList();
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
    Sesiones.Rotar(catalog, user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-disabled", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();

    if (teniaAlgo)
        try
        {
            await email.SendAsync(user.Email, user.Name, "Desactivaste la verificación en dos pasos en Aprendor",
                EmailTemplates.AuthenticatorChanged(user.Name, "disabled", DateTime.UtcNow));
        }
        catch (Exception ex) { logs.CreateLogger("DosFactores").LogWarning(ex, "No se pudo avisar de la baja de 2FA a {Email}", user.Email); }

    // Acaba de comprobar la contraseña actual: el token nuevo puede estrenar vida.
    var s = await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal, Amr.Pwd, conClave: true);
    return Results.Ok(new { mode = "none", confirmed = false, s.Token, s.Scope, s.User });
}).RequireAuthorization(PoliticasAcceso.Sesion);

// ---------- Recuperación de contraseña desde el login ----------
// Pedir el enlace. Responde SIEMPRE lo mismo y en el mismo tiempo, exista o no el correo:
// en los dos casos se busca el usuario, se cuentan sus solicitudes y se anota un
// SecurityEvent; el token y el correo se hacen en segundo plano (RestablecerWorker). Por
// cuenta: 3 por hora y 5 al día (las de más no hacen nada).
// Turnstile se valida antes de buscar el usuario. El admin de plataforma no recibe enlace
// por correo (respuesta igual y queda en la auditoría): lo restablece otro admin de
// plataforma o se vuelve a sembrar.
app.MapPost("/auth/forgot-password", async (ForgotPasswordRequest? req, HttpContext httpCtx, Turnstile turnstile,
    CatalogDbContext catalog, ColaRestablecer cola, IConfiguration cfg, ILoggerFactory logs, IWebHostEnvironment env) =>
{
    var verificacion = await turnstile.ValidarAsync(httpCtx, req?.TurnstileToken);
    if (verificacion != Turnstile.Resultado.Ok) return Turnstile.Respuesta(verificacion);

    var http = httpCtx.Request;
    var generico = Results.Ok(new { message = "Si el correo está registrado, te enviamos un enlace para restablecer la contraseña." });
    var correo = (req?.Email ?? "").Trim();
    if (correo.Length == 0 || correo.Length > 254) return generico;

    // Contar y anotar van juntos bajo un cerrojo por correo (CerrojoCuenta): con peticiones
    // en paralelo no se aceptan más de las que permite el tope. El cerrojo es por correo,
    // exista o no, para que el trabajo sea el mismo en los dos casos.
    Guid? id;
    bool pasado, plataforma;
    var recurso = "olvido:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(correo.ToLowerInvariant())));
    await using (var tx = await CerrojoCuenta.TomarAsync(catalog, recurso))
    {
        var ahora = DateTime.UtcNow;
        var fila = await catalog.Users.AsNoTracking().Where(u => u.Email == correo)
            .Select(u => new { u.Id, u.TenantId, u.Role }).FirstOrDefaultAsync();
        id = fila?.Id;
        plataforma = fila is not null && fila.TenantId == null && fila.Role == "Admin";
        var cuenta = id ?? Guid.Empty;
        var dia = ahora.AddDays(-1);
        var hora = ahora.AddHours(-1);
        // Cuentan las aceptadas y las denegadas al admin de plataforma: así su tope también
        // se aplica y, pasado, no se añaden más filas password-reset-denied a la auditoría.
        var previas = await catalog.SecurityEvents
            .Where(e => (e.Kind == EventosSeguridad.Restablecer || e.Kind == EventosSeguridad.RestablecerDenegado)
                        && e.UserId == cuenta && e.At > dia)
            .Select(e => e.At).ToListAsync();
        pasado = previas.Count(a => a > hora) >= 3 || previas.Count >= 5;
        EventosSeguridad.Anotar(catalog,
            pasado ? EventosSeguridad.RestablecerIgnorado
            : plataforma ? EventosSeguridad.RestablecerDenegado
            : EventosSeguridad.Restablecer, id);
        if (plataforma && !pasado)
            catalog.AuditLogs.Add(new CatalogAuditLog
            {
                Action = "password-reset-denied",
                Detail = $"{correo} (admin de plataforma: no se envía enlace por correo)",
                UserId = id
            });
        await catalog.SaveChangesAsync();
        await tx.CommitAsync();
    }
    if (id is null || pasado || plataforma) return generico;

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
// Turnstile se valida antes de buscar el token. Si la cuenta tiene app autenticadora y la
// petición no llega desde una red de confianza, exige también totpCode (cuenta para el
// bloqueo del doble factor); si falta, responde 400 { requiresTotp: true } SIN gastar el
// enlace. Tras restablecer, aviso a los Admin de la compañía (notifyAdmins) si fue desde
// fuera de sus redes de confianza.
app.MapPost("/auth/reset-password", async (ResetWithTokenRequest? req, HttpContext http, Turnstile turnstile,
    CatalogDbContext catalog, IMemoryCache cache, IEmailSender email, ILoggerFactory logs) =>
{
    var verificacion = await turnstile.ValidarAsync(http, req?.TurnstileToken);
    if (verificacion != Turnstile.Resultado.Ok) return Turnstile.Respuesta(verificacion);

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
    var invitacion = registro.Purpose == "invite";
    // El admin de plataforma no restablece por enlace (un enlace anterior a este cambio ya no vale).
    if (!invitacion && user.TenantId is null && user.Role == "Admin") return invalido;
    if (PoliticaClave.Validar(req!.NewPassword, user.Email) is string error) return Results.BadRequest(error);

    var log = logs.CreateLogger("PasswordReset");
    var ip = ClientIp.Of(http);
    var tieneApp = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null && !string.IsNullOrEmpty(user.TotpSecret);
    if (tieneApp && !await RedesConfianza.ParaUsuarioAsync(catalog, user, ip))
    {
        if (string.IsNullOrWhiteSpace(req.TotpCode))
            return Results.BadRequest(new { requiresTotp = true, error = "Escribe el código de tu app autenticadora para restablecer la contraseña." });
        if (Bloqueos.Espera(user.TwoFactorLockedUntil) is TimeSpan bloqueado) return Demasiados.Resultado(bloqueado);
        if (!await Bloqueos.Reservar2faAsync(catalog, user.Id))
            return Demasiados.Resultado(await Bloqueos.Espera2faAsync(catalog, user, email, log));
        var paso = Totp.Verificar(user.TotpSecret, req.TotpCode);
        if (paso is not long p || p <= (user.LastTotpStep ?? long.MinValue) || !await DosFactores.MarcarPasoAsync(catalog, user.Id, p))
        {
            var hasta = await Bloqueos.Fallo2faAsync(catalog, user, email, log);
            if (hasta is DateTime h) return Demasiados.Resultado(h - DateTime.UtcNow);
            return Results.BadRequest(new { requiresTotp = true, error = "El código de tu app autenticadora no es válido." });
        }
        await Bloqueos.Limpiar2faAsync(catalog, user.Id);
    }

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
    Sesiones.Rotar(catalog, user, cache);

    // El enlace llegó a su buzón: vale como la «prueba del correo» que se pide antes de
    // registrar la app autenticadora por primera vez fuera de las redes de confianza
    // (PruebaCorreo, 15 min desde esta IP). Así, al aceptar una invitación y activar el
    // doble factor enseguida, no se le vuelve a mandar un código por correo.
    if (!tieneApp) PruebaCorreo.Anotar(catalog, user.Id, ip);

    // Cualquier otro enlace pendiente de este usuario deja de servir.
    var otros = await catalog.PasswordResetTokens
        .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != registro.Id).ToListAsync();
    catalog.PasswordResetTokens.RemoveRange(otros);

    catalog.AuditLogs.Add(new CatalogAuditLog
    {
        Action = invitacion ? "invite-accepted" : "password-reset",
        Detail = $"{user.Email} ({ip}{(tieneApp ? ", con app autenticadora" : "")})",
        UserId = user.Id
    });
    await catalog.SaveChangesAsync();
    await Bloqueos.LimpiarLoginAsync(catalog, user.Id);   // por si el contador cambió después de leerlo
    if (!invitacion)
        await AvisosSeguridad.AdminsAsync(catalog, email, log, user, "password-reset", ip, soloFueraDeConfianza: true);
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
    // Se compara lo que escribió (no hace falta comprobar la clave para esto).
    if (req!.NewPassword == req.CurrentPassword)
        return Results.BadRequest("La nueva contraseña tiene que ser distinta de la actual.");

    // El intento se anota antes de comprobar la clave (bajo el cerrojo de la cuenta): con
    // peticiones en paralelo no se comprueban más de 5. El quinto fallo responde ya 429.
    var reserva = await Bloqueos.ReservarSensibleAsync(catalog, user.Id);
    if (reserva.Espera is TimeSpan espera) return Demasiados.Resultado(espera);
    if (!PasswordHasher.Verify(req.CurrentPassword, user.PasswordHash))
    {
        if (await Bloqueos.FalloSensibleAsync(catalog, user.Id) is TimeSpan tope) return Demasiados.Resultado(tope);
        return Results.BadRequest("La contraseña actual no es correcta.");
    }

    user.PasswordHash = PasswordHasher.Hash(req.NewPassword!);
    user.MustChangePassword = false;
    user.TempPasswordExpiresAt = null;
    Sesiones.Rotar(catalog, user, cache);
    Bloqueos.OkSensible(reserva);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "password-changed", Detail = user.Email, UserId = user.Id });
    await catalog.SaveChangesAsync();
    // Acaba de comprobar la contraseña actual: el token nuevo puede estrenar vida.
    return Results.Ok(await RespuestaSesion.RenovarAsync(catalog, jwt, user, principal, conClave: true));
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
// /admin/email-preview?kind=reminder|open|overdue|invite|reset|2fa|locked|2fa-locked|2fa-changed|recover|enroll-code|
//   2fa-reset|2fa-recovered|admin-reset-notice|admin-2fa-notice|completion|digest|certificate|certificate-officer|compliance|
//   retake|retake-void
app.MapGet("/admin/email-preview", (string? kind, DateTime? at) =>
{
    var k = (kind ?? "reminder").ToLowerInvariant();
    // ?at=2026-09-27T02:30:00Z: simula la hora «ahora» (sin zona se toma como UTC) para revisar
    // las fechas en la hora de la aplicación, p. ej. un certificado emitido de noche en Puerto Rico.
    var ahora = at is not DateTime a ? DateTime.UtcNow
        : a.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(a, DateTimeKind.Utc) : a.ToUniversalTime();
    var html = k switch
    {
        "open" => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "open", null, "https://aprendor.advancelogisticspr.com"),
        "invite" => EmailTemplates.InvitationLink("María Rivera", "maria.rivera@advancelogisticspr.com",
                    "https://aprendor.advancelogisticspr.com/index.html#invite=demo", Invitaciones.VigenciaHoras),
        "locked" => EmailTemplates.AccountLocked("María Rivera", "password", 15),
        "2fa-locked" => EmailTemplates.AccountLocked("María Rivera", "2fa", 15),
        "2fa-changed" => EmailTemplates.AuthenticatorChanged("María Rivera", "changed", ahora),
        "recover" => EmailTemplates.RecoveryCode("María Rivera", "428913", DosFactores.VigenciaMinutos),
        "enroll-code" => EmailTemplates.EnrollCode("María Rivera", "428913", DosFactores.VigenciaMinutos),
        "2fa-reset" => EmailTemplates.AuthenticatorReset("María Rivera", "admin", ahora),
        "2fa-recovered" => EmailTemplates.AuthenticatorReset("María Rivera", "recovered", ahora),
        "admin-reset-notice" => EmailTemplates.AdminSecurityNotice("José Torres", "María Rivera", "maria.rivera@advancelogisticspr.com",
                    "Advance Logistics", "password-reset", ahora, "203.0.113.7"),
        "admin-2fa-notice" => EmailTemplates.AdminSecurityNotice("José Torres", "María Rivera", "maria.rivera@advancelogisticspr.com",
                    "Advance Logistics", "2fa-recovered", ahora, "203.0.113.7"),
        "reset" => EmailTemplates.PasswordReset("María Rivera", "https://aprendor.advancelogisticspr.com/index.html#reset=demo", 60),
        "2fa" => EmailTemplates.TwoFactorCode("María Rivera", "428913", 10),
        "completion" => EmailTemplates.Completion("María Rivera", "Cumplimiento HIPAA para transporte y logística", 270, 300),
        "certificate" or "certificate-officer" => EmailTemplates.CertificateIssued("María Rivera",
                    "Cumplimiento HIPAA para transporte y logística", "CERT-2026-1A2B3C4D", ahora,
                    ahora.AddMonths(12), paraArchivo: k == "certificate-officer",
                    "https://aprendor.advancelogisticspr.com", link: "https://aprendor.advancelogisticspr.com/c/demo", dias: 30),
        "overdue" => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "overdue", ahora.AddDays(-9), "https://aprendor.advancelogisticspr.com"),
        "compliance" => EmailTemplates.ComplianceDigest("Advance Logistics",
                    new List<ComplianceRow> { new(Guid.NewGuid(), "María Rivera", "", new(), new() { "Choferes" }, Guid.NewGuid(),
                        "Cumplimiento HIPAA para transporte y logística", "expired", "overdue", ahora.AddDays(-3), "expires", 3) },
                    new List<ComplianceRow> { new(Guid.NewGuid(), "José Torres", "", new(), new() { "Almacén" }, Guid.NewGuid(),
                        "Hostigamiento sexual en el empleo", "overdue", "overdue", ahora.AddDays(-20), "due", 20) },
                    new List<ComplianceRow> { new(Guid.NewGuid(), "Ana López", "", new(), new(), Guid.NewGuid(),
                        "Seguridad de la Información para Empleados", "renewal", "due-soon", ahora.AddDays(12), "expires", 12) },
                    new List<ComplianceRow> { new(Guid.NewGuid(), "Luis Pérez", "", new(), new() { "Nuevos ingresos" }, Guid.NewGuid(),
                        "Ética Empresarial y Prevención de Fraude", "not-started", "not-started", ahora.AddDays(5), "due", 5) },
                    30, "https://aprendor.advancelogisticspr.com"),
        "retake" or "retake-void" => EmailTemplates.RetakeRequested("María Rivera",
                    "Cumplimiento HIPAA para transporte y logística", k == "retake-void" ? "void" : "renewal",
                    HoraLocal.FinDelDia(HoraLocal.DiaDe(ahora).AddDays(7)),
                    k == "retake-void" ? "El adiestramiento lo completó otra persona con su cuenta." : null,
                    "https://aprendor.advancelogisticspr.com").html,
        "digest" => EmailTemplates.Digest("María Rivera",
                    new List<PendingItem> { new(Guid.NewGuid(), "Ética Empresarial y Prevención de Fraude", Guid.NewGuid(), null, "not-started", null) },
                    new List<PendingItem> { new(Guid.NewGuid(), "Seguridad de la Información para Empleados", Guid.NewGuid(), null, "in-progress", null) }),
        _ => EmailTemplates.CourseReminder("María Rivera", "Cumplimiento HIPAA para transporte y logística",
                    "due15", ahora.AddDays(15), "https://aprendor.advancelogisticspr.com"),
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
//    (#invite=TOKEN, 72 h) para que la persona cree la suya. Si la invitación no sale
//    (invited = false), se genera además la clave temporal y se devuelve como abajo.
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
    {
        invited = await Invitaciones.EnviarAsync(catalog, user, EnlacesSeguridad.BaseUrl(cfg, http, env),
            email, cfg, env, logs.CreateLogger("Invitaciones"));
        if (!invited)
        {
            // La invitación no salió (sin App:BaseUrl, sin Email:ApiKey o falló el envío):
            // la cuenta no puede quedar sin ninguna forma de entrar. Se genera la clave
            // temporal como en el alta sin invitación y se devuelve UNA vez al admin. El
            // enlace, si llegó a crearse, sigue valiendo hasta que venza.
            temporal = PoliticaClave.Temporal();
            user.PasswordHash = PasswordHasher.Hash(temporal);
            user.MustChangePassword = true;
            user.TempPasswordExpiresAt = DateTime.UtcNow.Add(PoliticaClave.VigenciaTemporal);
            catalog.AuditLogs.Add(new CatalogAuditLog
            {
                Action = "user-temp-password",
                Detail = $"{user.Email} (la invitación no se envió: clave temporal)",
                UserId = tc.UserId
            });
            await catalog.SaveChangesAsync();
        }
    }
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
            .Select(u => new { u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword, TwoFactorEnabled = u.TwoFactorMode == "totp" && u.TwoFactorConfirmedAt != null, u.DeactivatedAt, u.DeactivationReason }).ToListAsync();
        var ids = gente.Select(u => u.Id).ToList();
        var aqui = await catalog.UserCompanies.Where(m => m.TenantId == suya && ids.Contains(m.UserId))
            .Select(m => new { m.Id, m.UserId, m.TenantId, m.Role, m.IsComplianceOfficer, m.DeactivatedAt }).ToListAsync();

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
                twoFactorEnabled = u.TwoFactorEnabled,
                isComplianceOfficer = m?.IsComplianceOfficer == true,
                // Desactivado aquí: la cuenta, o su membresía en esta compañía.
                deactivatedAt = u.DeactivatedAt ?? (principalAqui ? null : m?.DeactivatedAt),
                deactivationReason = u.DeactivationReason,
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
        .Select(u => new { u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword, TwoFactorEnabled = u.TwoFactorMode == "totp" && u.TwoFactorConfirmedAt != null, u.DeactivatedAt, u.DeactivationReason })
        .ToListAsync();

    // Membresías (UserCompany) de esos usuarios, con la marca de oficial de cumplimiento.
    var todos = users.Select(u => u.Id).ToList();
    var membresias = await catalog.UserCompanies.Where(m => todos.Contains(m.UserId))
        .Select(m => new { m.Id, m.UserId, m.TenantId, m.Role, m.IsComplianceOfficer, m.DeactivatedAt }).ToListAsync();

    return Results.Ok(users.Select(u =>
    {
        var compañia = tenantId ?? u.TenantId;
        var suyas = membresias.Where(m => m.UserId == u.Id).ToList();
        return new
        {
            u.Id, u.Email, u.Name, u.Role, u.TenantId, u.MustChangePassword,
            twoFactorEnabled = u.TwoFactorEnabled,
            deactivatedAt = u.DeactivatedAt ?? (tenantId is Guid ft && ft != u.TenantId ? suyas.FirstOrDefault(m => m.TenantId == ft)?.DeactivatedAt : null),
            deactivationReason = u.DeactivationReason,
            // Oficial de cumplimiento en la compañía filtrada (o, sin filtro, en su principal).
            isComplianceOfficer = suyas.Any(m => m.TenantId == compañia && m.IsComplianceOfficer),
            memberships = suyas.Select(m => new
            {
                membershipId = m.Id,
                tenantId = m.TenantId,
                role = m.TenantId == u.TenantId ? u.Role : m.Role,
                principal = m.TenantId == u.TenantId,
                isComplianceOfficer = m.IsComplianceOfficer,
                deactivatedAt = m.DeactivatedAt
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
    Sesiones.Rotar(catalog, user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "user-role", Detail = $"{user.Email} -> {req.Role}", UserId = tc.UserId });
    await catalog.SaveChangesAsync();   // Rotar vuelve a olvidar la caché al terminar de guardar
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
    Sesiones.Rotar(catalog, user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "admin-password-reset", Detail = user.Email, UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    await Bloqueos.LimpiarLoginAsync(catalog, user.Id);   // por si el contador cambió después de leerlo
    return Results.Ok(new { temporaryPassword = generada ? temporal : null, expiresAt = user.TempPasswordExpiresAt });
}).RequireAuthorization("Admin");

// Dar de baja a un usuario: DESACTIVA, nunca borra. Si después viene una auditoría, su
// expediente, sus intentos y sus certificados tienen que seguir ahí. La persona no puede
// entrar, sus sesiones se cierran y deja de contar en listas, cumplimiento, recordatorios
// y asignaciones; se puede reactivar. No puedes darte de baja a ti mismo.
// El Admin de una compañía no toca cuentas que también existen en otra: a quien llega por
// UserCompany solo lo desactiva en su compañía; a quien la tiene de principal pero
// pertenece a otras, no lo toca (lo hace el admin de plataforma).
// DELETE queda por compatibilidad y hace lo mismo que /deactivate sin motivo.
async Task<IResult> Desactivar(Guid id, string? motivo, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache)
{
    if (tc.UserId == id) return Results.BadRequest("No puedes darte de baja a ti mismo.");
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    if (!await AdminUsuarios.PuedeGestionarAsync(alcance, user, catalog)) return Results.Forbid();
    motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()[..Math.Min(motivo.Trim().Length, 300)];
    var ahora = DateTime.UtcNow;

    var membresias = await catalog.UserCompanies.Where(m => m.UserId == id).ToListAsync();
    if (alcance.Compañia is Guid suya)
    {
        if (user.TenantId != suya)
        {
            foreach (var m in membresias.Where(m => m.TenantId == suya)) m.DeactivatedAt ??= ahora;
            Sesiones.Rotar(catalog, user, cache);   // la baja de la membresía cierra sus sesiones
            catalog.AuditLogs.Add(new CatalogAuditLog { Action = "membership-deactivated", Detail = user.Email + (motivo is null ? "" : $" — {motivo}"), UserId = tc.UserId });
            await catalog.SaveChangesAsync();
            return Results.Ok(new { deactivatedMembership = true });
        }
        if (membresias.Any(m => m.TenantId != suya && m.DeactivatedAt == null))
            return Results.Conflict("Esta persona también pertenece a otra compañía; pide al administrador de la plataforma que la dé de baja.");
    }

    user.DeactivatedAt ??= ahora;
    user.DeactivationReason = motivo;
    // Invitaciones y enlaces de contraseña pendientes dejan de servir.
    catalog.PasswordResetTokens.RemoveRange(await catalog.PasswordResetTokens.Where(t => t.UserId == id && t.UsedAt == null).ToListAsync());
    Sesiones.Rotar(catalog, user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "user-deactivated", Detail = user.Email + (motivo is null ? "" : $" — {motivo}"), UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { deactivated = true });
}
app.MapPost("/admin/users/{id:guid}/deactivate", (Guid id, DeactivateRequest? req, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache) => Desactivar(id, req?.Reason, tc, principal, catalog, cache)).RequireAuthorization("Admin");
app.MapDelete("/admin/users/{id:guid}", (Guid id, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache) => Desactivar(id, null, tc, principal, catalog, cache)).RequireAuthorization("Admin");

// Reactivar: vuelve a contar y puede entrar con su contraseña de antes (si la olvidó,
// «¿Olvidaste tu contraseña?» o el admin le genera una temporal).
app.MapPost("/admin/users/{id:guid}/reactivate", async (Guid id, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache) =>
{
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    if (!await AdminUsuarios.PuedeGestionarAsync(alcance, user, catalog)) return Results.Forbid();

    if (alcance.Compañia is Guid suya && user.TenantId != suya)
    {
        foreach (var m in await catalog.UserCompanies.Where(m => m.UserId == id && m.TenantId == suya).ToListAsync())
            m.DeactivatedAt = null;
        catalog.AuditLogs.Add(new CatalogAuditLog { Action = "membership-reactivated", Detail = user.Email, UserId = tc.UserId });
        await catalog.SaveChangesAsync();
        return Results.Ok(new { reactivatedMembership = true });
    }
    user.DeactivatedAt = null;
    user.DeactivationReason = null;
    Sesiones.Rotar(catalog, user, cache);   // lo que se leyó de la cuenta mientras estaba desactivada, fuera
    catalog.AuditLogs.Add(new CatalogAuditLog { Action = "user-reactivated", Detail = user.Email, UserId = tc.UserId });
    await catalog.SaveChangesAsync();
    return Results.Ok(new { reactivated = true });
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
    if (await catalog.Users.FindAsync(userId) is AppUser afectado) Sesiones.Rotar(catalog, afectado, cache);
    else Sesiones.OlvidarAlGuardar(catalog, cache, userId);
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

// ---------- Seguridad de acceso de la compañía (bloque S3) ----------
// Redes de confianza (CIDR o IP sola), recuperación del doble factor por correo y avisos a
// los Admin. El Admin de la compañía, la suya (la activa del token); el admin de
// plataforma, cualquiera. La política de doble factor se sigue cambiando en
// /admin/tenants/{id}/two-factor (aquí solo se muestra). yourIp / yourIpTrusted: la IP con
// la que el servidor ve a quien llama y si hoy sería de confianza para esta compañía.
app.MapGet("/company/security", async (ITenantContext tc, HttpContext http, CatalogDbContext catalog) =>
{
    if (tc.TenantId is not Guid tid || tc.Role != "Admin") return Results.Forbid();
    var t = await catalog.Tenants.FindAsync(tid);
    return t is null ? Results.NotFound() : Results.Ok(SeguridadCompañia.Vista(t, ClientIp.Of(http), conInstancia: false));
}).RequireAuthorization("Admin");

app.MapPut("/company/security", async (SecurityConfigRequest? req, ITenantContext tc, HttpContext http, CatalogDbContext catalog) =>
{
    if (tc.TenantId is not Guid tid || tc.Role != "Admin") return Results.Forbid();
    var t = await catalog.Tenants.FindAsync(tid);
    if (t is null) return Results.NotFound();
    return await SeguridadCompañia.GuardarAsync(catalog, t, req, tc.UserId, ClientIp.Of(http), conInstancia: false);
}).RequireAuthorization("Admin");

app.MapGet("/admin/tenants/{id:guid}/security", async (Guid id, HttpContext http, CatalogDbContext catalog) =>
{
    var t = await catalog.Tenants.FindAsync(id);
    return t is null ? Results.NotFound() : Results.Ok(SeguridadCompañia.Vista(t, ClientIp.Of(http), conInstancia: true));
}).RequireAuthorization(AdminPlataforma.Politica);

app.MapPut("/admin/tenants/{id:guid}/security", async (Guid id, SecurityConfigRequest? req, ITenantContext tc,
    HttpContext http, CatalogDbContext catalog) =>
{
    var t = await catalog.Tenants.FindAsync(id);
    if (t is null) return Results.NotFound();
    return await SeguridadCompañia.GuardarAsync(catalog, t, req, tc.UserId, ClientIp.Of(http), conInstancia: true);
}).RequireAuthorization(AdminPlataforma.Politica);

// Reiniciar el doble factor de alguien que perdió su app: quita el autenticador, cierra
// sus sesiones (sello nuevo), audita y le avisa por correo. Al entrar, si su compañía lo
// exige, tendrá que registrar uno nuevo. El Admin de una compañía, sobre su gente (nunca
// un admin de plataforma, y si la persona pertenece también a otra compañía responde 409,
// igual que el restablecimiento de la clave); el admin de plataforma, sobre cualquiera que
// no sea admin de plataforma. Nadie sobre sí mismo (para eso está el perfil).
app.MapPost("/company/users/{id:guid}/reset-2fa", async (Guid id, ITenantContext tc, ClaimsPrincipal principal,
    CatalogDbContext catalog, IMemoryCache cache, IEmailSender email, ILoggerFactory logs) =>
{
    var alcance = await AdminUsuarios.AlcanceAsync(tc, principal, catalog, cache);
    if (alcance is null) return Results.Forbid();
    if (tc.UserId == id) return Results.BadRequest("Para cambiar tu propia verificación en dos pasos usa tu perfil.");
    var user = await catalog.Users.FindAsync(id);
    if (user is null) return Results.NotFound();
    if (user.TenantId is null && user.Role == "Admin") return Results.Forbid();   // admin de plataforma
    if (!await AdminUsuarios.PuedeGestionarAsync(alcance, user, catalog)) return Results.Forbid();
    if (alcance.Compañia is Guid suya && await AdminUsuarios.TieneOtraCompañiaAsync(user, suya, catalog))
        return Results.Conflict("Esta persona también pertenece a otra compañía; pide al administrador de la plataforma que reinicie su verificación en dos pasos.");

    var tenia = DobleFactor.Tiene(user);
    DobleFactor.Quitar(catalog, user, cache);
    catalog.AuditLogs.Add(new CatalogAuditLog
    {
        Action = "2fa-reset-by-admin",
        Detail = $"{user.Email}{(tenia ? "" : " (no tenía doble factor)")}",
        UserId = tc.UserId
    });
    await catalog.SaveChangesAsync();
    await Bloqueos.Limpiar2faAsync(catalog, user.Id);

    if (tenia)
        try
        {
            await email.SendAsync(user.Email, user.Name, "Se reinició tu verificación en dos pasos de Aprendor",
                EmailTemplates.AuthenticatorReset(user.Name, "admin", DateTime.UtcNow));
        }
        catch (Exception ex) { logs.CreateLogger("DosFactores").LogWarning(ex, "No se pudo avisar del reinicio del 2FA a {Email}", user.Email); }
    return Results.Ok(new { reset = true, hadTwoFactor = tenia });
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
    // hasCover: el curso tiene foto de portada (GET /media/portada/{id}, ver Portadas.cs).
    var conPortada = await TrainingPlatform.Portadas.ConPortadaAsync(db, ContenidoAcceso.PuedeCrear(tc.Role));
    if (ContenidoAcceso.PuedeCrear(tc.Role))
    {
        var todos = await db.Trainings.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Results.Ok(todos.Select(t =>
        {
            var n = System.Text.Json.JsonSerializer.SerializeToNode(t, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.AsObject();
            n["hasCover"] = conPortada.Contains(t.Id);
            return n;
        }));
    }

    var publicados = await db.Trainings
        .Where(t => t.Status != "archived"
            && db.TrainingVersions.Any(v => v.TrainingId == t.Id && v.Status == "published"))
        .OrderByDescending(t => t.CreatedAt)
        .Select(t => new { t.Id, t.Title, t.Description, t.CategoryId, t.Status, t.PlayerConfigJson })
        .ToListAsync();
    return Results.Ok(publicados.Select(t => new
    {
        t.Id, t.Title, t.Description, t.CategoryId, t.Status,
        playerConfigJson = ContenidoAcceso.SoloPortada(t.PlayerConfigJson),
        hasCover = conPortada.Contains(t.Id)
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
app.MapNarracion();
app.MapPortadas();
app.MapCertificates();
app.MapCompliance();
app.MapRetakes();

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
        if (user.DeactivatedAt is not null) return lista;   // cuenta desactivada: ninguna

        if (user.TenantId is Guid principal)
        {
            var t = await catalog.Tenants.FirstOrDefaultAsync(x => x.Id == principal && x.Status == "active");
            if (t is not null) lista.Add(new Membresia(t.Id, t.Name, user.Role, true, t.TwoFactorPolicy));
        }

        var extras = await (from m in catalog.UserCompanies
                            where m.UserId == user.Id && m.DeactivatedAt == null
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
    // Los administradores (de plataforma, sin compañía, o Admin en alguna compañía) llevan
    // SIEMPRE doble factor, sin importar la política de sus compañías: son las cuentas que
    // más daño pueden hacer si alguien adivina o roba la contraseña.
    public static bool EsAdministrador(AppUser user, IEnumerable<Membresia> compañias)
        => user.Role == "Admin" || compañias.Any(c => c.Rol == "Admin");

    public static string PoliticaParaUsuario(AppUser user, IEnumerable<Membresia> compañias)
        => EsAdministrador(user, compañias) ? "required" : PoliticaEfectiva(compañias);

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
    // envioContado: quien llama ya reservó y anotó el envío (Bloqueos.ReservarEnvioCodigoAsync).
    public static async Task<TwoFactorChallenge> AbrirRetoAsync(CatalogDbContext catalog, AppUser user,
        string proposito, IEmailSender email, ILoggerFactory logs, IConfiguration cfg, string? modoForzado = null,
        Guid? destino = null, DateTime? sesionHasta = null, bool envioContado = false)
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
        if (modo == "email" && !envioContado) EventosSeguridad.Anotar(catalog, EventosSeguridad.CodigoEnviado, user.Id);
        await catalog.SaveChangesAsync();

        if (modo == "email" && codigo is not null)
        {
            var (asunto, html) = proposito switch
            {
                "verify-email" => (EmailTemplates.AsuntoConCodigo(codigo, "tu código para validar tu correo en Aprendor"), EmailTemplates.VerifyEmail(user.Name, codigo, VigenciaMinutos)),
                "recover-2fa" => (EmailTemplates.AsuntoConCodigo(codigo, "tu código para recuperar tu acceso a Aprendor"), EmailTemplates.RecoveryCode(user.Name, codigo, VigenciaMinutos)),
                "enroll-2fa" => (EmailTemplates.AsuntoConCodigo(codigo, "tu código para registrar tu app autenticadora en Aprendor"), EmailTemplates.EnrollCode(user.Name, codigo, VigenciaMinutos)),
                _ => (EmailTemplates.AsuntoConCodigo(codigo, "tu código de verificación de Aprendor"), EmailTemplates.TwoFactorCode(user.Name, codigo, VigenciaMinutos)),
            };
            try
            {
                await email.SendAsync(user.Email, user.Name, asunto, html);
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
    // de 5 por reto. Se reserva igual en el contador del usuario (todos los retos, TOTP y
    // códigos por correo): a los 5 fallos, el doble factor queda bloqueado 15 minutos. Un código
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

        // Y en el usuario (todos sus retos): también se reserva antes de verificar, así que
        // ráfagas en paralelo sobre retos distintos no pasan de 5 códigos entre todas.
        if (!await Bloqueos.Reservar2faAsync(catalog, user.Id))
        {
            var e = await Bloqueos.Espera2faAsync(catalog, user, email, log);
            return new(false, null, null, Demasiados.Mensaje(e), e);
        }

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
    public static bool PuedeCrear(string? rol) => rol is "Admin" or "Author";

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
    public static readonly string[] Roles = { "Admin", "Author", "Learner" };   // el rol Moderator se retiró (sep 2026)

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
        return await Membresias.EsMiembroAsync(catalog, tid, u.Id, incluirDesactivados: true);
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
record LoginRequest(string? Email, string? Password, string? TurnstileToken = null);
record CreateTenantRequest(string Name, string? ConnectionString = null, string? DatabaseName = null);
record CreateUserRequest(string Email, string Name, string? Password, string Role, Guid? TenantId, bool? SendInvite);
record CreateCategoryRequest(string Name, Guid? ParentId);
record CreateTrainingRequest(string Title, string? Description, Guid? CategoryId);
record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
record RoleRequest(string Role);
record ResetPasswordRequest(string? TempPassword);
record ForgotPasswordRequest(string? Email, string? TurnstileToken = null);
record ResetWithTokenRequest(string? Token, string? NewPassword, string? TotpCode = null, string? TurnstileToken = null);
record RecoverStartRequest(Guid ChallengeId, string? TurnstileToken = null);
record TwoFactorVerifyRequest(Guid ChallengeId, string? Code);
// /me/email/verify: el reto puede faltar (la página se recargó y el envío del código
// respondió 429, por ejemplo). Si el correo ya está validado no hace falta; si no, el
// error dice qué hacer en vez de un 400 vacío del enlace del modelo.
record EmailVerifyRequest(Guid? ChallengeId, string? Code);
record ResendRequest(Guid ChallengeId);
record TwoFactorSetupRequest(string? Mode, string? CurrentPassword = null, string? Code = null,
    Guid? EmailChallengeId = null, string? EmailCode = null);
record TwoFactorConfirmRequest(string? Mode, string? Code, Guid? ChallengeId);
record SwitchCompanyRequest(Guid TenantId);
record TwoFactorPolicyRequest(string Policy);
record CompanyMembershipRequest(Guid UserId, Guid TenantId, string Role);
record ComplianceOfficerRequest(bool IsOfficer, Guid? TenantId = null);
record DisableTwoFactorRequest(string? CurrentPassword);
record StatusRequest(string Status);
record DeactivateRequest(string? Reason);
