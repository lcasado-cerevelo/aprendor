using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
// UPDATE atómico) y el intento se RESERVA antes de comprobar la clave o el código: un
// UPDATE condicionado suma 1 solo si la cuenta no está bloqueada y no hay ya 5 intentos
// sin resolver. Así las peticiones en paralelo no comprueban más de 5 claves (o códigos)
// entre bloqueo y bloqueo: la sexta no llega a comprobarse. Acertar deja el contador en 0.
public static class Bloqueos
{
    public const int MaxFallos = 5;
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(15);

    // ---- Contraseña en /auth/login: 5 fallos seguidos → 15 minutos ----

    // true = intento reservado (se puede comprobar la clave). false = bloqueada o con 5
    // intentos en curso: no se comprueba nada (ver EsperaLoginAsync).
    public static async Task<bool> ReservarLoginAsync(CatalogDbContext c, Guid userId)
    {
        var ahora = DateTime.UtcNow;
        return await c.Users
            .Where(x => x.Id == userId && (x.LockoutEnd == null || x.LockoutEnd <= ahora) && x.AccessFailedCount < MaxFallos)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AccessFailedCount, x => x.AccessFailedCount + 1)) > 0;
    }

    // Clave mala (el intento ya se contó al reservarlo). Devuelve el fin del bloqueo si la
    // cuenta quedó bloqueada (por este fallo o por otra petición a la vez): el quinto
    // fallo responde ya 429.
    public static async Task<DateTime?> FalloLoginAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        EventosSeguridad.Anotar(c, EventosSeguridad.LoginFallido, u.Id);
        return await BloquearLoginSiTocaAsync(c, u, email, log);
    }

    // Si el contador llegó al tope, bloquea y lo deja en 0 (un solo UPDATE condicionado:
    // de varias peticiones a la vez, solo una aplica el bloqueo, audita y avisa).
    private static async Task<DateTime?> BloquearLoginSiTocaAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        var hasta = DateTime.UtcNow.Add(Duracion);
        var n = await c.Users.Where(x => x.Id == u.Id && x.AccessFailedCount >= MaxFallos)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockoutEnd, (DateTime?)hasta).SetProperty(x => x.AccessFailedCount, 0));
        if (n == 0)
        {
            await c.SaveChangesAsync();
            // Otra petición en paralelo pudo bloquear ya la cuenta: se responde igual (429).
            var fin = await c.Users.Where(x => x.Id == u.Id).Select(x => x.LockoutEnd).FirstOrDefaultAsync();
            return Espera(fin) is null ? null : fin;
        }

        EventosSeguridad.Anotar(c, EventosSeguridad.LoginBloqueado, u.Id);
        c.AuditLogs.Add(new CatalogAuditLog { Action = "account-locked", Detail = u.Email, UserId = u.Id });
        await c.SaveChangesAsync();
        await AvisarAsync(c, u, "password", email, log);
        return hasta;
    }

    // La reserva falló: cuánto esperar. Si el contador quedó en el tope sin bloqueo (5
    // intentos en curso, o uno que se cortó a medias), el bloqueo se aplica aquí; si la
    // clave buena llega entre esos intentos, al entrar lo levanta.
    public static async Task<TimeSpan> EsperaLoginAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        if (await BloquearLoginSiTocaAsync(c, u, email, log) is DateTime h) return h - DateTime.UtcNow;
        var fin = await c.Users.Where(x => x.Id == u.Id).Select(x => x.LockoutEnd).FirstOrDefaultAsync();
        return Espera(fin) ?? TimeSpan.FromMinutes(1);
    }

    // Correo que no existe: el mismo trabajo de BD que un fallo real (la reserva y el
    // UPDATE del bloqueo, que no tocan ninguna fila, el SecurityEvent y la lectura del
    // bloqueo), para que el tiempo de respuesta no lo delate.
    public static async Task FalloLoginDesconocidoAsync(CatalogDbContext c)
    {
        await ReservarLoginAsync(c, Guid.Empty);
        var hasta = DateTime.UtcNow.Add(Duracion);
        await c.Users.Where(x => x.Id == Guid.Empty && x.AccessFailedCount >= MaxFallos)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockoutEnd, (DateTime?)hasta).SetProperty(x => x.AccessFailedCount, 0));
        await EventosSeguridad.RegistrarAsync(c, EventosSeguridad.LoginFallido, null);
        await c.Users.Where(x => x.Id == Guid.Empty).Select(x => x.LockoutEnd).FirstOrDefaultAsync();
    }

    // Al entrar bien, el contador vuelve a 0 (incluido el intento reservado).
    public static Task<int> LimpiarLoginAsync(CatalogDbContext c, Guid userId)
        => c.Users.Where(x => x.Id == userId && (x.AccessFailedCount != 0 || x.LockoutEnd != null))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AccessFailedCount, 0).SetProperty(x => x.LockoutEnd, (DateTime?)null));

    // ---- Segundo factor: 5 códigos fallidos sumando todos los retos → 15 minutos ----
    // Igual que el login: el intento se reserva en el usuario antes de verificar el
    // código, así que las ráfagas en paralelo de varios retos no pasan de 5 entre todas.

    public static async Task<bool> Reservar2faAsync(CatalogDbContext c, Guid userId)
    {
        var ahora = DateTime.UtcNow;
        return await c.Users
            .Where(x => x.Id == userId && (x.TwoFactorLockedUntil == null || x.TwoFactorLockedUntil <= ahora) && x.TwoFactorFailedCount < MaxFallos)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorFailedCount, x => x.TwoFactorFailedCount + 1)) > 0;
    }

    // Código malo (ya contado al reservarlo). Devuelve el fin del bloqueo si el 2FA quedó
    // bloqueado (por este fallo o por otra petición a la vez).
    public static async Task<DateTime?> Fallo2faAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        EventosSeguridad.Anotar(c, EventosSeguridad.SegundoFactorFallido, u.Id);
        return await Bloquear2faSiTocaAsync(c, u, email, log);
    }

    private static async Task<DateTime?> Bloquear2faSiTocaAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        var hasta = DateTime.UtcNow.Add(Duracion);
        var n = await c.Users.Where(x => x.Id == u.Id && x.TwoFactorFailedCount >= MaxFallos)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorLockedUntil, (DateTime?)hasta).SetProperty(x => x.TwoFactorFailedCount, 0));
        if (n == 0)
        {
            await c.SaveChangesAsync();
            // Otra petición en paralelo pudo bloquear ya el 2FA: se responde igual (429).
            var fin = await c.Users.Where(x => x.Id == u.Id).Select(x => x.TwoFactorLockedUntil).FirstOrDefaultAsync();
            return Espera(fin) is null ? null : fin;
        }

        EventosSeguridad.Anotar(c, EventosSeguridad.SegundoFactorBloqueado, u.Id);
        c.AuditLogs.Add(new CatalogAuditLog { Action = "2fa-locked", Detail = u.Email, UserId = u.Id });
        await c.SaveChangesAsync();
        await AvisarAsync(c, u, "2fa", email, log);
        return hasta;
    }

    // La reserva del 2FA falló: cuánto esperar (y bloqueo si el contador quedó en el tope).
    public static async Task<TimeSpan> Espera2faAsync(CatalogDbContext c, AppUser u, IEmailSender email, ILogger log)
    {
        if (await Bloquear2faSiTocaAsync(c, u, email, log) is DateTime h) return h - DateTime.UtcNow;
        var fin = await c.Users.Where(x => x.Id == u.Id).Select(x => x.TwoFactorLockedUntil).FirstOrDefaultAsync();
        return Espera(fin) ?? TimeSpan.FromMinutes(1);
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
    // 5 fallos (desde el último acierto) en 15 minutos → 15 minutos de espera. Se cuentan
    // filas de SecurityEvent, así que contar y anotar van juntos bajo un cerrojo de la
    // cuenta (CerrojoCuenta): el intento se anota como fallido ANTES de comprobar la clave
    // y, si resulta buena, OkSensible lo convierte en acierto. Con peticiones en paralelo,
    // la sexta ya ve los cinco intentos anotados y no llega a comprobarse.

    public sealed record ReservaSensible(TimeSpan? Espera, SecurityEvent? Evento);

    public static async Task<ReservaSensible> ReservarSensibleAsync(CatalogDbContext c, Guid userId)
    {
        await using var tx = await CerrojoCuenta.TomarAsync(c, $"sensible:{userId}");
        if (await EsperaSensibleAsync(c, userId) is TimeSpan espera) return new(espera, null);
        var ev = new SecurityEvent
        {
            Kind = EventosSeguridad.ClaveSensibleFallida, UserId = userId,
            Ip = AuditoriaIp.Actual, At = DateTime.UtcNow
        };
        c.SecurityEvents.Add(ev);
        await c.SaveChangesAsync();
        await tx.CommitAsync();
        return new(null, ev);
    }

    // No era buena: el fallo ya quedó anotado al reservar. Devuelve la espera si con este
    // fallo se llegó al tope (el quinto fallo responde ya 429).
    public static Task<TimeSpan?> FalloSensibleAsync(CatalogDbContext c, Guid userId) => EsperaSensibleAsync(c, userId);

    // Era buena: el intento reservado pasa a acierto (se guarda con el próximo SaveChanges).
    public static void OkSensible(ReservaSensible r)
    {
        if (r.Evento is null) return;
        r.Evento.Kind = EventosSeguridad.ClaveSensibleOk;
        r.Evento.At = DateTime.UtcNow;
    }

    private static async Task<TimeSpan?> EsperaSensibleAsync(CatalogDbContext c, Guid userId)
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

    // ---- Envío de códigos por correo (/auth/2fa/resend, /me/email/send-code) ----
    // 60 segundos entre envíos y 10 al día por cuenta. Contar y anotar el envío van juntos
    // bajo el cerrojo de la cuenta: dos peticiones a la vez no mandan dos correos.
    // null = envío reservado y ya anotado (AbrirRetoAsync con envioContado: true).

    public static async Task<TimeSpan?> ReservarEnvioCodigoAsync(CatalogDbContext c, Guid userId)
    {
        await using var tx = await CerrojoCuenta.TomarAsync(c, $"codigos:{userId}");
        var ahora = DateTime.UtcNow;
        var dia = ahora.AddDays(-1);
        var envios = await c.SecurityEvents
            .Where(e => e.Kind == EventosSeguridad.CodigoEnviado && e.UserId == userId && e.At > dia)
            .Select(e => e.At).ToListAsync();
        if (envios.Count >= 10) return envios.Min().AddDays(1) - ahora;
        if (envios.Count > 0 && ahora - envios.Max() < TimeSpan.FromSeconds(60))
            return envios.Max().AddSeconds(60) - ahora;

        EventosSeguridad.Anotar(c, EventosSeguridad.CodigoEnviado, userId);
        await c.SaveChangesAsync();
        await tx.CommitAsync();
        return null;
    }
}

// Cerrojo por cuenta en SQL Server (sp_getapplock, con la transacción como dueña) para los
// topes que cuentan filas de SecurityEvent: contar y anotar pasa dentro del cerrojo, así
// que las peticiones en paralelo (también desde varias instancias de la app) van de una
// en una. Se suelta al confirmar o deshacer la transacción que devuelve.
public static class CerrojoCuenta
{
    public static async Task<IDbContextTransaction> TomarAsync(CatalogDbContext c, string recurso)
    {
        var tx = await c.Database.BeginTransactionAsync();
        try
        {
            var resultado = new SqlParameter("@r", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await c.Database.ExecuteSqlRawAsync(
                "EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                new SqlParameter("@recurso", SqlDbType.NVarChar, 255) { Value = recurso }, resultado);
            if (resultado.Value is not int r || r < 0)
                throw new TimeoutException($"No se pudo tomar el cerrojo {recurso} (sp_getapplock devolvió {resultado.Value}).");
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
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
//
// Invalidar la caché: además de borrar la entrada, se deja una marca de tiempo por usuario
// (Olvidar). Una lectura del catálogo que empezó ANTES de la marca no vale aunque se haya
// guardado en la caché después (una petición con el token viejo que leyó el sello antiguo
// justo antes del SaveChanges): la siguiente validación la descarta y vuelve a leer.
public static class Sesiones
{
    private static readonly TimeSpan Cache = TimeSpan.FromSeconds(60);
    // Más que la vida de una entrada de caché: toda entrada leída antes de la marca caduca
    // antes de que la marca desaparezca.
    private static readonly TimeSpan VidaMarca = TimeSpan.FromMinutes(3);

    private sealed record Instantanea(Guid Sello, string Rol, Guid? Principal, Dictionary<Guid, string> Membresias, long LeidaEn);

    private static string Clave(Guid userId) => $"sesion:{userId}";
    private static string Marca(Guid userId) => $"sesion-cambio:{userId}";

    // Cierra todas las sesiones del usuario: sello nuevo, que se guarda con el próximo
    // SaveChanges de quien llama. La caché se olvida ahora y otra vez cuando ese
    // SaveChanges termina (ya con el sello nuevo en la BD).
    public static void Rotar(CatalogDbContext c, AppUser u, IMemoryCache cache)
    {
        u.SecurityStamp = Guid.NewGuid();
        OlvidarAlGuardar(c, cache, u.Id);
    }

    // Para cambios de membresía o rol que se guardan después: olvida ahora y tras el
    // próximo SaveChanges del contexto.
    public static void OlvidarAlGuardar(CatalogDbContext c, IMemoryCache cache, Guid userId)
    {
        Olvidar(cache, userId);
        EventHandler<SavedChangesEventArgs>? alGuardar = null;
        alGuardar = (_, _) =>
        {
            c.SavedChanges -= alGuardar;
            Olvidar(cache, userId);
        };
        c.SavedChanges += alGuardar;
    }

    // Tras cambios ya guardados (membresías, roles) o al borrar la cuenta.
    public static void Olvidar(IMemoryCache cache, Guid userId)
    {
        cache.Set(Marca(userId), Stopwatch.GetTimestamp(), VidaMarca);
        cache.Remove(Clave(userId));
        AdminPlataforma.Olvidar(cache, userId);
    }

    private static async Task<Instantanea?> LeerAsync(IServiceProvider sp, Guid uid)
    {
        var leidaEn = Stopwatch.GetTimestamp();   // antes de consultar: cuenta el inicio de la lectura
        var catalog = sp.GetRequiredService<CatalogDbContext>();
        var u = await catalog.Users.AsNoTracking().Where(x => x.Id == uid)
            .Select(x => new { x.SecurityStamp, x.Role, x.TenantId }).FirstOrDefaultAsync();
        if (u is null) return null;
        var ms = await catalog.UserCompanies.AsNoTracking().Where(m => m.UserId == uid)
            .Select(m => new { m.TenantId, m.Role }).ToListAsync();
        return new Instantanea(u.SecurityStamp, u.Role, u.TenantId,
            ms.GroupBy(m => m.TenantId).ToDictionary(g => g.Key, g => g.First().Role), leidaEn);
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
        var inst = await cache.GetOrCreateAsync(Clave(uid), e =>
        {
            e.AbsoluteExpirationRelativeToNow = Cache;
            return LeerAsync(sp, uid);
        });

        // Leída antes del último cambio de la cuenta: no sirve, se vuelve a leer.
        if (inst is not null && cache.TryGetValue(Marca(uid), out long marca) && inst.LeidaEn <= marca)
        {
            inst = await LeerAsync(sp, uid);
            if (inst is null) cache.Remove(Clave(uid));
            else cache.Set(Clave(uid), inst, Cache);
        }

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

    public static Task<SesionEmitida> CrearAsync(CatalogDbContext catalog, JwtTokenService jwt, AppUser user,
        string amr, Guid? tenantId = null, DateTime? expiraAntesDe = null)
        => EmitirAsync(catalog, jwt, user, amr, tenantId, _ => expiraAntesDe);

    // tope: dado el alcance nuevo, hasta cuándo puede valer como mucho el token (null = su vida normal).
    private static async Task<SesionEmitida> EmitirAsync(CatalogDbContext catalog, JwtTokenService jwt, AppUser user,
        string amr, Guid? tenantId, Func<string, DateTime?> tope)
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
            jwt.Create(user, compañia, rol, scope, amr, tope(scope)),
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
    // Por defecto NO alarga la sesión: el token nuevo vence cuando vencía el actual. Solo
    // estrena su vida normal:
    //  - conClave: la petición acaba de comprobar la contraseña actual (lo mismo que entrar);
    //  - si el token actual es restringido y el alcance cambia: se completó el paso pendiente
    //    (un token restringido sale de un login de hace menos de 15 minutos y el paso no se
    //    puede repetir sin otra prueba).
    // Así, con un token robado no se puede encadenar renovaciones para no dejarlo vencer.
    public static Task<SesionEmitida> RenovarAsync(CatalogDbContext catalog, JwtTokenService jwt, AppUser user,
        System.Security.Claims.ClaimsPrincipal actual, string? amr = null, bool conClave = false)
    {
        Guid? tid = Guid.TryParse(actual.FindFirst("tenant_id")?.Value, out var t) ? t : null;
        var amrActual = amr ?? actual.FindFirst("amr")?.Value ?? Amr.Pwd;
        var alcanceActual = actual.FindFirst("scope")?.Value ?? Alcances.Full;
        var expira = ExpiracionDe(actual);
        return EmitirAsync(catalog, jwt, user, amrActual, tid ?? user.TenantId, nuevo =>
            conClave || (alcanceActual != Alcances.Full && nuevo != alcanceActual) ? null : expira);
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
