using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TrainingPlatform.Auth;
using TrainingPlatform.Catalog;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.Notifications;

namespace TrainingPlatform.Seguridad;

// ============================================================================
// Autenticación y sesión (bloque S2): límites por IP, bloqueos por cuenta, sello de
// seguridad, política de contraseñas, restablecimiento en segundo plano e invitaciones.
// ============================================================================

// Tipos de SecurityEvent. Se cuentan por cuenta (UserId) o por IP, con índices
// (Kind, UserId, At) y (Kind, Ip, At).
public static class EventosSeguridad
{
    public const string LoginOk = "login-ok";
    public const string LoginFallido = "login-failed";
    public const string LoginBloqueado = "login-locked";
    public const string SegundoFactorFallido = "2fa-failed";
    public const string SegundoFactorBloqueado = "2fa-locked";
    public const string AvisoBloqueo = "lockout-email";
    public const string Restablecer = "forgot-password";               // solicitud aceptada (cuenta para el tope)
    public const string RestablecerIgnorado = "forgot-password-limited"; // pasó del tope: no se envió nada
    public const string CodigoEnviado = "code-sent";
    public const string ClaveSensibleFallida = "password-check-failed";
    public const string ClaveSensibleOk = "password-check-ok";

    // Añade el evento al contexto (se guarda con el próximo SaveChanges). La IP, si no se
    // da, es la de la petición en curso (AuditoriaIp).
    public static void Anotar(CatalogDbContext c, string kind, Guid? userId, Guid? tenantId = null, string? ip = null)
        => c.SecurityEvents.Add(new SecurityEvent
        {
            Kind = kind, UserId = userId, TenantId = tenantId,
            Ip = ip ?? AuditoriaIp.Actual, At = DateTime.UtcNow
        });

    public static async Task RegistrarAsync(CatalogDbContext c, string kind, Guid? userId, Guid? tenantId = null)
    {
        Anotar(c, kind, userId, tenantId);
        await c.SaveChangesAsync();
    }
}

// Respuesta 429 uniforme: cabecera Retry-After (segundos) y cuerpo
// { error: "Demasiados intentos. Espera N minutos.", minutes: N }.
public static class Demasiados
{
    public static int Minutos(TimeSpan espera) => Math.Max(1, (int)Math.Ceiling(espera.TotalMinutes));

    public static string Mensaje(TimeSpan espera)
    {
        var n = Minutos(espera);
        return $"Demasiados intentos. Espera {n} {(n == 1 ? "minuto" : "minutos")}.";
    }

    public static IResult Resultado(TimeSpan espera, string? mensaje = null) => new Resultado429(espera, mensaje);

    public static async Task EscribirAsync(HttpContext ctx, TimeSpan espera, string? mensaje = null)
    {
        if (espera < TimeSpan.Zero) espera = TimeSpan.Zero;
        ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)).ToString();
        ctx.Response.Headers.CacheControl = "no-store";
        await ctx.Response.WriteAsJsonAsync(new { error = mensaje ?? Mensaje(espera), minutes = Minutos(espera) });
    }

    private sealed class Resultado429 : IResult
    {
        private readonly TimeSpan _espera;
        private readonly string? _mensaje;
        public Resultado429(TimeSpan espera, string? mensaje) { _espera = espera; _mensaje = mensaje; }
        public Task ExecuteAsync(HttpContext httpContext) => EscribirAsync(httpContext, _espera, _mensaje);
    }
}

// Límites por IP real (ClientIp.Of) en los puntos de acceso anónimos o que mandan correo.
// Ventana fija por (regla, IP); con el mismo "cubo" dos rutas comparten contador.
// /c/{token} tiene su propio límite (Certificates.cs), también con la IP real.
public static class LimitesPorIp
{
    private sealed record Regla(string Ruta, string Cubo, int Permisos, TimeSpan Ventana);

    private static readonly Regla[] Primarias =
    {
        new("/auth/login",               "login",   10, TimeSpan.FromMinutes(1)),
        new("/auth/2fa/verify",          "2fa",     10, TimeSpan.FromMinutes(1)),
        new("/auth/2fa/recover/verify",  "2fa",     10, TimeSpan.FromMinutes(1)),   // bloque S3
        new("/auth/2fa/recover/start",   "recover",  5, TimeSpan.FromHours(1)),     // bloque S3
        new("/auth/forgot-password",     "forgot",   5, TimeSpan.FromHours(1)),
        new("/auth/reset-password",      "reset",   10, TimeSpan.FromHours(1)),
        new("/auth/2fa/resend",          "codes",   10, TimeSpan.FromHours(1)),
        new("/me/email/send-code",       "codes",   10, TimeSpan.FromHours(1)),
    };

    // Segundo nivel, encadenado: además de 10/min, el login no pasa de 50 cada 15 min.
    private static readonly Regla[] Secundarias =
    {
        new("/auth/login", "login-15m", 50, TimeSpan.FromMinutes(15)),
    };

    public static IServiceCollection AddLimitesPorIp(this IServiceCollection services)
        => services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.CreateChained(Crear(Primarias), Crear(Secundarias));
            o.OnRejected = async (ctx, ct) =>
            {
                var espera = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra)
                    ? ra
                    : (Buscar(Secundarias, ctx.HttpContext) ?? Buscar(Primarias, ctx.HttpContext))?.Ventana ?? TimeSpan.FromMinutes(1);
                ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("LimitesPorIp")
                    .LogWarning("Límite por IP alcanzado en {Ruta} desde {Ip}", ctx.HttpContext.Request.Path, ClientIp.Of(ctx.HttpContext));
                await Demasiados.EscribirAsync(ctx.HttpContext, espera);
            };
        });

    private static PartitionedRateLimiter<HttpContext> Crear(Regla[] reglas)
        => PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var r = Buscar(reglas, ctx);
            if (r is null) return RateLimitPartition.GetNoLimiter("");
            return RateLimitPartition.GetFixedWindowLimiter($"{r.Cubo}|{ClientIp.Of(ctx)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = r.Permisos,
                Window = r.Ventana,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });

    private static Regla? Buscar(Regla[] reglas, HttpContext ctx)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method)) return null;
        var ruta = (ctx.Request.Path.Value ?? "").TrimEnd('/');
        foreach (var r in reglas)
            if (string.Equals(ruta, r.Ruta, StringComparison.OrdinalIgnoreCase)) return r;
        return null;
    }
}

// Bloqueos por cuenta. Los contadores de AppUser se tocan con ExecuteUpdateAsync (un
// UPDATE atómico): dos peticiones en paralelo no pueden saltarse el tope.
public static class Bloqueos
{
    public const int MaxFallos = 5;
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(15);

    // ---- Contraseña en /auth/login: 5 fallos seguidos → 15 minutos ----

    // Devuelve el fin del bloqueo si ESTE fallo lo activó (null si no).
    public static async Task<DateTime?> FalloLoginAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        var hasta = DateTime.UtcNow.Add(Duracion);
        await SumarFalloLoginAsync(c, u.Id, hasta);
        EventosSeguridad.Anotar(c, EventosSeguridad.LoginFallido, u.Id);
        // Según el contador leído al entrar: si otra petición en paralelo sumó a la vez, el
        // bloqueo se aplica igual (el UPDATE es atómico) y el 429 llega en el siguiente intento.
        if (u.AccessFailedCount + 1 < MaxFallos) { await c.SaveChangesAsync(); return null; }

        EventosSeguridad.Anotar(c, EventosSeguridad.LoginBloqueado, u.Id);
        c.AuditLogs.Add(new CatalogAuditLog { Action = "account-locked", Detail = u.Email, UserId = u.Id });
        await c.SaveChangesAsync();
        await AvisarAsync(c, u, "password", email, log);
        return hasta;
    }

    // Un solo UPDATE: suma el fallo y, si llega al tope, bloquea y deja el contador en 0
    // (en SQL las dos expresiones ven el valor ANTERIOR de AccessFailedCount).
    private static Task<int> SumarFalloLoginAsync(CatalogDbContext c, Guid userId, DateTime hasta)
        => c.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.LockoutEnd, x => x.AccessFailedCount + 1 >= MaxFallos ? (DateTime?)hasta : x.LockoutEnd)
            .SetProperty(x => x.AccessFailedCount, x => x.AccessFailedCount + 1 >= MaxFallos ? 0 : x.AccessFailedCount + 1));

    // Correo que no existe: el mismo trabajo de BD que un fallo real (un UPDATE que no toca
    // ninguna fila y el SecurityEvent), para que el tiempo de respuesta no lo delate.
    public static async Task FalloLoginDesconocidoAsync(CatalogDbContext c)
    {
        await SumarFalloLoginAsync(c, Guid.Empty, DateTime.UtcNow.Add(Duracion));
        await EventosSeguridad.RegistrarAsync(c, EventosSeguridad.LoginFallido, null);
    }

    // Al entrar bien, el contador vuelve a 0.
    public static async Task LimpiarLoginAsync(CatalogDbContext c, AppUser u)
    {
        if (u.AccessFailedCount == 0 && u.LockoutEnd is null) return;
        await c.Users.Where(x => x.Id == u.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AccessFailedCount, 0).SetProperty(x => x.LockoutEnd, (DateTime?)null));
    }

    // ---- Segundo factor: 5 códigos fallidos sumando todos los retos → 15 minutos ----

    public static async Task<DateTime?> Fallo2faAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        await c.Users.Where(x => x.Id == u.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorFailedCount, x => x.TwoFactorFailedCount + 1));
        var hasta = DateTime.UtcNow.Add(Duracion);
        var n = await c.Users.Where(x => x.Id == u.Id && x.TwoFactorFailedCount >= MaxFallos)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorLockedUntil, (DateTime?)hasta).SetProperty(x => x.TwoFactorFailedCount, 0));

        EventosSeguridad.Anotar(c, EventosSeguridad.SegundoFactorFallido, u.Id);
        if (n == 0) { await c.SaveChangesAsync(); return null; }

        EventosSeguridad.Anotar(c, EventosSeguridad.SegundoFactorBloqueado, u.Id);
        c.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-locked", Detail = u.Email, UserId = u.Id });
        await c.SaveChangesAsync();
        await AvisarAsync(c, u, "2fa", email, log);
        return hasta;
    }

    public static async Task<int> Restantes2faAsync(CatalogDbContext c, Guid userId)
        => MaxFallos - await c.Users.Where(x => x.Id == userId).Select(x => x.TwoFactorFailedCount).FirstOrDefaultAsync();

    public static async Task Limpiar2faAsync(CatalogDbContext c, Guid userId)
        => await c.Users.Where(x => x.Id == userId && (x.TwoFactorFailedCount != 0 || x.TwoFactorLockedUntil != null))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorFailedCount, 0).SetProperty(x => x.TwoFactorLockedUntil, (DateTime?)null));

    public static TimeSpan? Espera(DateTime? hasta)
        => hasta is DateTime h && h > DateTime.UtcNow ? h - DateTime.UtcNow : null;

    // Correo al usuario cuando su cuenta se bloquea: como mucho uno por hora.
    private static async Task AvisarAsync(CatalogDbContext c, AppUser u, string motivo, IEmailSender email, ILogger log)
    {
        var desde = DateTime.UtcNow.AddHours(-1);
        if (await c.SecurityEvents.AnyAsync(e => e.Kind == EventosSeguridad.AvisoBloqueo && e.UserId == u.Id && e.At > desde))
            return;
        await EventosSeguridad.RegistrarAsync(c, EventosSeguridad.AvisoBloqueo, u.Id);
        try
        {
            await email.SendAsync(u.Email, u.Name, "Bloqueamos temporalmente el acceso a tu cuenta de Aprendor",
                EmailTemplates.AccountLocked(u.Name, motivo, (int)Duracion.TotalMinutes));
        }
        catch (Exception ex) { log.LogWarning(ex, "No se pudo avisar del bloqueo a {Email}", u.Email); }
    }

    // ---- Comprobaciones de la contraseña actual con sesión abierta ----
    // /me/password, /me/2fa/disable, /me/2fa/setup (con clave) y /me/2fa/confirm:
    // 5 fallos (desde el último acierto) en 15 minutos → 15 minutos de espera.

    public static async Task<TimeSpan?> EsperaSensibleAsync(CatalogDbContext c, Guid userId)
    {
        var ahora = DateTime.UtcNow;
        var desde = ahora.AddMinutes(-2 * Duracion.TotalMinutes);
        var ultimoOk = await c.SecurityEvents
            .Where(e => e.Kind == EventosSeguridad.ClaveSensibleOk && e.UserId == userId && e.At > desde)
            .MaxAsync(e => (DateTime?)e.At);
        if (ultimoOk is DateTime ok) desde = ok;
        var fallos = await c.SecurityEvents
            .Where(e => e.Kind == EventosSeguridad.ClaveSensibleFallida && e.UserId == userId && e.At > desde)
            .OrderByDescending(e => e.At).Select(e => e.At).Take(MaxFallos).ToListAsync();
        if (fallos.Count < MaxFallos) return null;
        if (fallos[0] - fallos[^1] > Duracion) return null;       // no fueron seguidos
        var libre = fallos[0].Add(Duracion);
        return libre > ahora ? libre - ahora : null;
    }

    public static Task FalloSensibleAsync(CatalogDbContext c, Guid userId)
        => EventosSeguridad.RegistrarAsync(c, EventosSeguridad.ClaveSensibleFallida, userId);

    public static void OkSensible(CatalogDbContext c, Guid userId)
        => EventosSeguridad.Anotar(c, EventosSeguridad.ClaveSensibleOk, userId);

    // ---- Envío de códigos por correo (/auth/2fa/resend, /me/email/send-code) ----
    // 60 segundos entre envíos y 10 al día por cuenta.

    public static async Task<TimeSpan?> EsperaEnvioCodigoAsync(CatalogDbContext c, Guid userId)
    {
        var ahora = DateTime.UtcNow;
        var dia = ahora.AddDays(-1);
        var envios = await c.SecurityEvents
            .Where(e => e.Kind == EventosSeguridad.CodigoEnviado && e.UserId == userId && e.At > dia)
            .Select(e => e.At).ToListAsync();
        if (envios.Count >= 10) return envios.Min().AddDays(1) - ahora;
        if (envios.Count > 0 && ahora - envios.Max() < TimeSpan.FromSeconds(60))
            return envios.Max().AddSeconds(60) - ahora;
        return null;
    }
}

// Política única de contraseñas: la aplican todos los puntos que fijan una clave
// (restablecer, invitación, /me/password, clave temporal del admin).
public static class PoliticaClave
{
    public const int Minimo = 10;
    public const int Maximo = 128;

    // Lista corta de contraseñas comunes de 10 o más caracteres (las más cortas ya las
    // rechaza el mínimo). Se compara sin mayúsculas y sin espacios a los lados.
    private static readonly HashSet<string> Comunes = new(StringComparer.OrdinalIgnoreCase)
    {
        "1234567890", "0123456789", "0987654321", "12345678910", "123456789a", "a123456789",
        "1q2w3e4r5t", "1qaz2wsx3edc", "qwertyuiop", "qwerty1234", "qwerty12345", "asdfghjkl1",
        "password12", "password123", "password1234", "password!1", "passw0rd123", "p@ssw0rd123",
        "contraseña", "contraseña1", "contraseña123", "contrasena", "contrasena1", "contrasena123",
        "changeme123", "changeme123!", "cambiame123", "bienvenido", "bienvenido1", "bienvenido123",
        "bienvenida1", "welcome123", "welcome1234", "iloveyou12", "iloveyou123", "teamo12345",
        "admin12345", "admin123456", "administrador", "administrator", "aprendor123", "aprendor2026",
        "puertorico1", "puertorico123", "abcdefghij", "abc1234567", "abcd123456", "letmein123",
        "sunshine123", "princesa123", "football123", "baseball123", "superman123", "temporal123",
        "temporal2026", "temporal2026!", "verano2026", "invierno2026", "primavera2026", "otoño2026",
    };

    // null = válida; si no, el motivo en español.
    public static string? Validar(string? clave, string? correo)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length < Minimo)
            return $"La contraseña debe tener al menos {Minimo} caracteres.";
        if (clave.Length > Maximo)
            return $"La contraseña no puede pasar de {Maximo} caracteres.";

        var c = clave.Trim();
        var mail = (correo ?? "").Trim();
        if (mail.Length > 0)
        {
            var local = mail.Split('@')[0];
            if (string.Equals(c, mail, StringComparison.OrdinalIgnoreCase) || string.Equals(c, local, StringComparison.OrdinalIgnoreCase))
                return "La contraseña no puede ser tu correo.";
        }
        if (Comunes.Contains(c) || c.Distinct().Count() <= 2)
            return "Esa contraseña es demasiado común o fácil de adivinar. Escoge otra.";
        return null;
    }

    // Clave temporal de 16 caracteres (sin caracteres que se confunden al dictarla),
    // con al menos una mayúscula, una minúscula, un dígito y un símbolo.
    public static string Temporal()
    {
        const string mayus = "ABCDEFGHJKLMNPQRSTUVWXYZ", minus = "abcdefghijkmnopqrstuvwxyz",
                     digitos = "23456789", simbolos = "!@#$%*?-";
        var todos = (mayus + minus + digitos + simbolos).ToCharArray();
        while (true)
        {
            var s = new string(RandomNumberGenerator.GetItems<char>(todos, 16));
            if (s.Any(mayus.Contains) && s.Any(minus.Contains) && s.Any(digitos.Contains) && s.Any(simbolos.Contains))
                return s;
        }
    }

    public static readonly TimeSpan VigenciaTemporal = TimeSpan.FromHours(72);
}

// Sello de seguridad (AppUser.SecurityStamp, claim "sst"). En cada petición autenticada
// (JwtBearerEvents.OnTokenValidated) se comprueba contra el catálogo, con caché de 60 s,
// que el usuario existe, que el sello coincide, que sigue perteneciendo a la compañía del
// token y que su rol ahí es el del token. Si no, la petición no se autentica (401).
public static class Sesiones
{
    private static readonly TimeSpan Cache = TimeSpan.FromSeconds(60);

    private sealed record Instantanea(Guid Sello, string Rol, Guid? Principal, Dictionary<Guid, string> Membresias);

    private static string Clave(Guid userId) => $"sesion:{userId}";

    // Cierra todas las sesiones del usuario: sello nuevo (se guarda con el SaveChanges de
    // quien llama) y se olvida la caché para que surta efecto en la próxima petición.
    public static void Rotar(AppUser u, IMemoryCache cache)
    {
        u.SecurityStamp = Guid.NewGuid();
        Olvidar(cache, u.Id);
    }

    // Tras cambiar membresías o roles sin rotar el sello, o al borrar la cuenta.
    public static void Olvidar(IMemoryCache cache, Guid userId)
    {
        cache.Remove(Clave(userId));
        AdminPlataforma.Olvidar(cache, userId);
    }

    public static async Task ValidarAsync(TokenValidatedContext ctx)
    {
        var p = ctx.Principal;
        if (p is null
            || !Guid.TryParse(p.FindFirst("sub")?.Value, out var uid)
            || !Guid.TryParse(p.FindFirst("sst")?.Value, out var sello)
            || string.IsNullOrEmpty(p.FindFirst("scope")?.Value))
        {
            ctx.Fail("Sesión inválida: vuelve a iniciar sesión.");
            return;
        }

        var sp = ctx.HttpContext.RequestServices;
        var cache = sp.GetRequiredService<IMemoryCache>();
        var inst = await cache.GetOrCreateAsync(Clave(uid), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = Cache;
            var catalog = sp.GetRequiredService<CatalogDbContext>();
            var u = await catalog.Users.AsNoTracking().Where(x => x.Id == uid)
                .Select(x => new { x.SecurityStamp, x.Role, x.TenantId }).FirstOrDefaultAsync();
            if (u is null) return null;
            var ms = await catalog.UserCompanies.AsNoTracking().Where(m => m.UserId == uid)
                .Select(m => new { m.TenantId, m.Role }).ToListAsync();
            return new Instantanea(u.SecurityStamp, u.Role, u.TenantId,
                ms.GroupBy(m => m.TenantId).ToDictionary(g => g.Key, g => g.First().Role));
        });

        if (inst is null || inst.Sello != sello)
        {
            ctx.Fail("La sesión ya no es válida: vuelve a iniciar sesión.");
            return;
        }

        // Rol vigente en la compañía del token: el de la cuenta si es su principal, el de
        // la membresía si no. Sin compañía en el token: solo si la cuenta no tiene principal.
        string? rolActual;
        var tidTexto = p.FindFirst("tenant_id")?.Value;
        if (tidTexto is null) rolActual = inst.Principal is null ? inst.Rol : null;
        else if (!Guid.TryParse(tidTexto, out var tid)) rolActual = null;
        else if (inst.Principal == tid) rolActual = inst.Rol;
        else rolActual = inst.Membresias.TryGetValue(tid, out var r) ? r : null;

        if (rolActual is null || rolActual != p.FindFirst("role")?.Value)
            ctx.Fail("Tu acceso cambió: vuelve a iniciar sesión.");
    }
}

// Respuesta de una sesión nueva: { token, scope, user { ... } }, con los mismos campos
// que el login devolvía siempre (el front los usa), más scope y amr.
public sealed record SesionEmitida(string Token, string Scope, object User);

public static class RespuestaSesion
{
    // Vencimiento del token actual (claim "exp"): el cambio de compañía no lo extiende.
    public static DateTime ExpiracionDe(System.Security.Claims.ClaimsPrincipal p)
        => long.TryParse(p.FindFirst("exp")?.Value, out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
            : DateTime.UtcNow;

    public static async Task<SesionEmitida> CrearAsync(CatalogDbContext catalog, JwtTokenService jwt, AppUser user,
        string amr, Guid? tenantId = null, DateTime? expiraAntesDe = null)
    {
        var compañias = await Compañias.DeUsuarioAsync(catalog, user);
        var politica = Compañias.PoliticaEfectiva(compañias);
        var compañia = tenantId ?? user.TenantId;
        var rol = compañia is Guid t && t != user.TenantId
            ? compañias.FirstOrDefault(c => c.TenantId == t)?.Rol ?? user.Role
            : user.Role;
        var scope = Alcances.Calcular(user, politica);
        var tiene2fa = user.TwoFactorMode == "totp" && user.TwoFactorConfirmedAt is not null;

        return new SesionEmitida(
            jwt.Create(user, compañia, rol, scope, amr, expiraAntesDe),
            scope,
            new
            {
                id = user.Id, email = user.Email, name = user.Name, role = rol, tenantId = compañia,
                mustChangePassword = user.MustChangePassword, twoFactorMode = user.TwoFactorMode,
                emailVerified = user.EmailVerifiedAt is not null,
                twoFactorPolicy = politica,
                mustEnroll2fa = politica == "required" && !tiene2fa,
                isComplianceOfficer = await ComplianceOfficers.EsOficialAsync(catalog, user.Id, compañia),
                companies = compañias,
                scope, amr
            });
    }

    // La misma sesión (compañía y amr) con un token nuevo: tras rotar el sello o completar
    // un paso pendiente (cambio de clave, correo, 2FA). El alcance se vuelve a calcular.
    public static Task<SesionEmitida> RenovarAsync(CatalogDbContext catalog, JwtTokenService jwt, AppUser user,
        System.Security.Claims.ClaimsPrincipal actual, string? amr = null)
    {
        Guid? tid = Guid.TryParse(actual.FindFirst("tenant_id")?.Value, out var t) ? t : null;
        var amrActual = amr ?? actual.FindFirst("amr")?.Value ?? Amr.Pwd;
        return CrearAsync(catalog, jwt, user, amrActual, tid ?? user.TenantId);
    }
}

// URL base de los enlaces de los correos de seguridad: App:BaseUrl; solo en Development,
// si está vacía, el origen de la petición (fuera, la cabecera Host no es de fiar).
public static class EnlacesSeguridad
{
    public static string? BaseUrl(IConfiguration cfg, HttpRequest http, IHostEnvironment env)
    {
        var baseUrl = (cfg["App:BaseUrl"] ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) && env.IsDevelopment()) baseUrl = $"{http.Scheme}://{http.Host}{http.PathBase}";
        return string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl;
    }
}

// ---- "¿Olvidaste tu contraseña?" en segundo plano ----
// El endpoint hace el mismo trabajo de BD exista o no el correo y responde enseguida;
// crear el token y hablar con Brevo pasa aquí, fuera de la petición (así el tiempo de
// respuesta no delata qué correos existen).
public sealed record SolicitudRestablecer(Guid UserId, string BaseUrl);

public sealed class ColaRestablecer
{
    private readonly Channel<SolicitudRestablecer> _canal = Channel.CreateBounded<SolicitudRestablecer>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool Encolar(SolicitudRestablecer s) => _canal.Writer.TryWrite(s);
    public ChannelReader<SolicitudRestablecer> Lector => _canal.Reader;
}

public sealed class RestablecerWorker : BackgroundService
{
    // Enlaces vivos por usuario: con el tope de 3 por hora ya no hace falta invalidar el
    // anterior en cada solicitud (así un tercero no puede anular el enlace de la víctima).
    private const int MaxVivos = 3;

    private readonly ColaRestablecer _cola;
    private readonly IServiceScopeFactory _scopes;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;
    private readonly ILogger<RestablecerWorker> _log;

    public RestablecerWorker(ColaRestablecer cola, IServiceScopeFactory scopes, IEmailSender email,
        IConfiguration cfg, IHostEnvironment env, ILogger<RestablecerWorker> log)
    {
        _cola = cola; _scopes = scopes; _email = email; _cfg = cfg; _env = env; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var s in _cola.Lector.ReadAllAsync(stoppingToken))
        {
            try { await ProcesarAsync(s); }
            catch (Exception ex) { _log.LogError(ex, "Falló el envío del enlace de recuperación al usuario {UserId}", s.UserId); }
        }
    }

    private async Task ProcesarAsync(SolicitudRestablecer s)
    {
        using var scope = _scopes.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var user = await catalog.Users.FindAsync(s.UserId);
        if (user is null) return;

        var ahora = DateTime.UtcNow;
        var vivos = await catalog.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Purpose == "reset")
            .OrderByDescending(t => t.CreatedAt).ToListAsync();
        catalog.PasswordResetTokens.RemoveRange(vivos.Where((t, i) => t.ExpiresAt <= ahora || i >= MaxVivos - 1));

        var (token, hash) = ResetTokens.Create();
        catalog.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            Purpose = "reset",
            CreatedAt = ahora,
            ExpiresAt = ahora.AddMinutes(ResetTokens.VigenciaMinutos)
        });
        await catalog.SaveChangesAsync();

        // El token va en el fragmento (#reset=): el navegador nunca lo manda al servidor.
        var enlace = $"{s.BaseUrl}/index.html#reset={token}";
        try
        {
            await _email.SendAsync(user.Email, user.Name, "Restablecer tu contraseña de Aprendor",
                EmailTemplates.PasswordReset(user.Name, enlace, ResetTokens.VigenciaMinutos));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo enviar el correo de recuperación a {Email}", user.Email);
        }
        // Sin la API de correo el enlace solo queda en el log en desarrollo.
        if (string.IsNullOrWhiteSpace(_cfg["Email:ApiKey"]))
        {
            if (_env.IsDevelopment())
                _log.LogWarning("Email:ApiKey no está configurado: no se envió correo. Enlace de recuperación para {Email}: {Enlace}",
                    user.Email, enlace);
            else
                _log.LogError("Email:ApiKey no está configurado: no se envió el correo de recuperación a {Email}.", user.Email);
        }
    }
}

// ---- Invitación: enlace de un solo uso (#invite=TOKEN, 72 h) para crear la clave ----
public static class Invitaciones
{
    public const int VigenciaHoras = 72;

    // Crea el token (invalida invitaciones anteriores sin usar) y manda el correo.
    // Devuelve true si el correo salió (con la API de correo configurada).
    public static async Task<bool> EnviarAsync(CatalogDbContext catalog, AppUser user, string? baseUrl,
        IEmailSender email, IConfiguration cfg, IHostEnvironment env, ILogger log)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            log.LogError("App:BaseUrl no está configurado: no se envió la invitación a {Email}.", user.Email);
            return false;
        }

        var ahora = DateTime.UtcNow;
        var previas = await catalog.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Purpose == "invite").ToListAsync();
        catalog.PasswordResetTokens.RemoveRange(previas);

        var (token, hash) = ResetTokens.Create();
        catalog.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            Purpose = "invite",
            CreatedAt = ahora,
            ExpiresAt = ahora.AddHours(VigenciaHoras)
        });
        await catalog.SaveChangesAsync();

        var enlace = $"{baseUrl}/index.html#invite={token}";
        try
        {
            await email.SendAsync(user.Email, user.Name, "Invitación a Aprendor",
                EmailTemplates.InvitationLink(user.Name, user.Email, enlace, VigenciaHoras));
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "No se pudo enviar la invitación a {Email}", user.Email);
            return false;
        }
        if (string.IsNullOrWhiteSpace(cfg["Email:ApiKey"]))
        {
            if (env.IsDevelopment())
                log.LogWarning("Email:ApiKey no está configurado: no se envió la invitación. Enlace para {Email}: {Enlace}",
                    user.Email, enlace);
            else
                log.LogError("Email:ApiKey no está configurado: no se envió la invitación a {Email}.", user.Email);
            return false;
        }
        return true;
    }
}
