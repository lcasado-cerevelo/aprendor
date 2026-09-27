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
    // Bloque S3
    public const string RestablecerDenegado = "forgot-password-denied";  // admin de plataforma: no se envía enlace
    public const string RecuperacionEnviada = "2fa-recover-sent";        // código de «Perdí mi autenticador»
    public const string PruebaCorreo = "email-proof";                    // demostró tener el correo (código de alta o de recuperación)

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
        new("/me/2fa/setup/send-code",   "codes",   10, TimeSpan.FromHours(1)),     // bloque S3
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

    // tipoExtra/maxExtraDia: un tope diario propio además del general, contado y anotado
    // bajo el mismo cerrojo (el código de «Perdí mi autenticador»: 3 al día).
    public static async Task<TimeSpan?> ReservarEnvioCodigoAsync(CatalogDbContext c, Guid userId,
        string? tipoExtra = null, int maxExtraDia = 0)
    {
        await using var tx = await CerrojoCuenta.TomarAsync(c, $"codigos:{userId}");
        var ahora = DateTime.UtcNow;
        var dia = ahora.AddDays(-1);
        if (tipoExtra is not null)
        {
            var extra = await c.SecurityEvents
                .Where(e => e.Kind == tipoExtra && e.UserId == userId && e.At > dia)
                .Select(e => e.At).ToListAsync();
            if (extra.Count >= maxExtraDia) return extra.Count == 0 ? TimeSpan.FromDays(1) : extra.Min().AddDays(1) - ahora;
        }
        var envios = await c.SecurityEvents
            .Where(e => e.Kind == EventosSeguridad.CodigoEnviado && e.UserId == userId && e.At > dia)
            .Select(e => e.At).ToListAsync();
        if (envios.Count >= 10) return envios.Min().AddDays(1) - ahora;
        if (envios.Count > 0 && ahora - envios.Max() < TimeSpan.FromSeconds(60))
            return envios.Max().AddSeconds(60) - ahora;

        EventosSeguridad.Anotar(c, EventosSeguridad.CodigoEnviado, userId);
        if (tipoExtra is not null) EventosSeguridad.Anotar(c, tipoExtra, userId);
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
        var politica = Compañias.PoliticaParaUsuario(user, compañias);
        var compañia = tenantId ?? user.TenantId;
        var rol = compañia is Guid t && t != user.TenantId
            ? compañias.FirstOrDefault(c => c.TenantId == t)?.Rol ?? user.Role
            : user.Role;
        // Sesión abierta desde una red de confianza (amr=mfa-trusted): no se exige el alta
        // del doble factor aunque alguna compañía lo pida (bloque S3).
        var exigeAlta = politica == "required" && amr != Amr.MfaTrusted;
        var scope = Alcances.Calcular(user, politica == "required" && !exigeAlta ? "optional" : politica);
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
                mustEnroll2fa = exigeAlta && !tiene2fa,
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

// ============================================================================
// Bloque S3: redes de confianza, Turnstile, «Perdí mi autenticador», reinicio del
// doble factor y avisos a los Admin de la compañía.
// ============================================================================

// Redes de confianza: desde ellas no se pide el doble factor ni su alta (el token lleva
// amr=mfa-trusted) y, si son de la instancia, tampoco Turnstile. Son las de la instancia
// (Security:TrustedNetworks) más las de cada compañía (Tenant.SecurityConfigJson,
// trustedNetworks). Loopback NO es de confianza salvo que se liste: detrás del túnel todo
// llega desde loopback y la IP que cuenta es la real (ClientIp.Of).
public static class RedesConfianza
{
    private static IReadOnlyList<Cidr> _instancia = Array.Empty<Cidr>();
    public static IReadOnlyList<Cidr> Instancia => _instancia;

    // Al arrancar: lee Security:TrustedNetworks (arreglo o lista separada por comas); las
    // que no se entienden se ignoran con una advertencia en la bitácora.
    public static void Configurar(IConfiguration cfg, ILogger log)
    {
        var lista = new List<Cidr>();
        foreach (var s in ArranqueSeguro.Lista(cfg, "Security:TrustedNetworks"))
        {
            if (Cidr.TryParse(s, out var c, out var error)) lista.Add(c);
            else log.LogWarning("Security:TrustedNetworks: se ignora «{Red}» ({Motivo}).", s, error);
        }
        _instancia = lista;
        if (lista.Count > 0)
            log.LogInformation("Redes de confianza de la instancia: {Redes}", string.Join(", ", lista));
    }

    public static bool En(IEnumerable<Cidr> redes, string? ip)
        => System.Net.IPAddress.TryParse(ip ?? "", out var dir) && redes.Any(r => r.Contiene(dir));

    public static bool EnInstancia(string? ip) => En(_instancia, ip);

    // Para una compañía: la instancia más las redes de esa compañía.
    public static async Task<bool> ParaCompañiaAsync(CatalogDbContext c, Guid tenantId, string? ip)
    {
        if (EnInstancia(ip)) return true;
        var json = await c.Tenants.AsNoTracking().Where(t => t.Id == tenantId)
            .Select(t => t.SecurityConfigJson).FirstOrDefaultAsync();
        return json is not null && En(SecurityConfig.Parse(json).Redes(), ip);
    }

    // Para una persona: la IP es de confianza si está en las redes de la instancia o en las
    // de TODAS sus compañías que usan el doble factor (política distinta de off; si ninguna
    // lo usa, todas sus compañías). Así una compañía no puede quitarle el doble factor que
    // le exige otra. Sin compañías (admin de plataforma): solo las de la instancia.
    // Los administradores (de plataforma o de cualquier compañía) nunca están en red de
    // confianza: siempre se les pide el código y el alta del doble factor.
    public static async Task<bool> ParaUsuarioAsync(CatalogDbContext c, AppUser u, string? ip)
    {
        var compañias = await Compañias.DeUsuarioAsync(c, u);
        if (Compañias.EsAdministrador(u, compañias)) return false;
        if (EnInstancia(ip)) return true;
        if (!System.Net.IPAddress.TryParse(ip ?? "", out _)) return false;
        var relevantes = compañias.Where(x => x.Politica2FA != "off").Select(x => x.TenantId).ToList();
        if (relevantes.Count == 0) relevantes = compañias.Select(x => x.TenantId).ToList();
        if (relevantes.Count == 0) return false;
        var jsons = await c.Tenants.AsNoTracking().Where(t => relevantes.Contains(t.Id))
            .Select(t => t.SecurityConfigJson).ToListAsync();
        return jsons.Count == relevantes.Count && jsons.All(j => En(SecurityConfig.Parse(j).Redes(), ip));
    }

    // ¿Hay alguna red desde la que no se le pediría el código? Las de la instancia, o las de
    // TODAS sus compañías relevantes (como en ParaUsuarioAsync). Solo para el texto del
    // alta: «al entrar desde fuera de la oficina» en vez de «cada vez que entres».
    public static async Task<bool> HayParaUsuarioAsync(CatalogDbContext c, AppUser u)
    {
        var compañias = await Compañias.DeUsuarioAsync(c, u);
        if (Compañias.EsAdministrador(u, compañias)) return false;   // a ellos siempre se les pide
        if (_instancia.Count > 0) return true;
        var relevantes = compañias.Where(x => x.Politica2FA != "off").Select(x => x.TenantId).ToList();
        if (relevantes.Count == 0) relevantes = compañias.Select(x => x.TenantId).ToList();
        if (relevantes.Count == 0) return false;
        var jsons = await c.Tenants.AsNoTracking().Where(t => relevantes.Contains(t.Id))
            .Select(t => t.SecurityConfigJson).ToListAsync();
        return jsons.Count == relevantes.Count && jsons.All(j => SecurityConfig.Parse(j).Redes().Count > 0);
    }
}

// Cloudflare Turnstile: se valida en el servidor ANTES de buscar el usuario en
// /auth/login, /auth/forgot-password, /auth/reset-password y /auth/2fa/recover/start.
// Encendido solo con Turnstile:Enabled = true y las dos claves: si falta la secreta
// (APRENDOR_Turnstile__SecretKey) queda APAGADO (no se valida y /auth/config no da la
// site key), para que un despliegue sin la variable no bloquee la entrada; al arrancar
// se deja una advertencia. Desde las redes de confianza de la instancia no se pide.
// Turnstile:ExemptNetworks (vacío por defecto; se activa listándolo a propósito) quita
// SOLO Turnstile, y solo a peticiones sin token que llegan DIRECTAS desde esas redes (sin
// cabeceras de proxy): sirve para que los seeds y tools/Seed-Curso.ps1 entren por la API
// desde el propio servidor (http://localhost:8086) y el front de desarrollo funcione sin
// widget. No es red de confianza: el doble factor se sigue pidiendo. Con token se valida.
public sealed class Turnstile
{
    public enum Resultado { Ok, Invalido, NoDisponible }

    public const string ClienteHttp = "turnstile";
    private const string UrlVerificar = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<Turnstile> _log;
    private readonly IReadOnlyList<Cidr> _exentas;

    public Turnstile(IHttpClientFactory http, IConfiguration cfg, ILogger<Turnstile> log)
    {
        _http = http; _cfg = cfg; _log = log;
        var exentas = new List<Cidr>();
        foreach (var s in ArranqueSeguro.Lista(cfg, "Turnstile:ExemptNetworks"))
        {
            if (Cidr.TryParse(s, out var c, out var error)) exentas.Add(c);
            else log.LogWarning("Turnstile:ExemptNetworks: se ignora «{Red}» ({Motivo}).", s, error);
        }
        _exentas = exentas;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private bool Encendido => bool.TryParse(_cfg["Turnstile:Enabled"], out var b) && b;
    private string? SiteKey => Limpio(_cfg["Turnstile:SiteKey"]);
    private string? SecretKey => Limpio(_cfg["Turnstile:SecretKey"]);

    public bool Activo => Encendido && SiteKey is not null && SecretKey is not null;

    // Cabeceras que ponen los proxies y túneles (cloudflared, Cloudflare, IIS ARR, nginx) o
    // que deja UseForwardedHeaders al aplicarlas (X-Original-*). Con cualquiera de ellas la
    // petición no llega directa, aunque el socket venga de una red exenta: lo que entra por
    // el túnel sale de loopback, pero Cloudflare siempre añade CF-Ray y CF-Connecting-IP.
    private static readonly string[] CabecerasProxy =
    {
        "X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host", "Forwarded",
        "X-Original-For", "X-Original-Proto", "X-Original-Host",
        "CF-Connecting-IP", "CF-Ray", "CF-Visitor", "True-Client-IP", "X-Real-IP", "X-ARR-LOG-ID"
    };

    // ¿Petición directa (el socket, no una cabecera) desde una red de Turnstile:ExemptNetworks?
    private bool DirectaDesdeExenta(HttpContext ctx)
    {
        if (_exentas.Count == 0) return false;
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (!_exentas.Any(r => r.Contiene(ip))) return false;
        var cabeceras = ctx.Request.Headers;
        return !CabecerasProxy.Any(h => cabeceras.ContainsKey(h));
    }

    public void AdvertirAlArrancar()
    {
        if (!Encendido) { _log.LogInformation("Turnstile apagado (Turnstile:Enabled = false)."); return; }
        if (SecretKey is null)
            _log.LogWarning("Turnstile:Enabled está en true pero falta Turnstile:SecretKey (variable APRENDOR_Turnstile__SecretKey): " +
                            "Turnstile queda APAGADO y el acceso no pide la verificación. Define la variable y reinicia.");
        else if (SiteKey is null)
            _log.LogWarning("Turnstile:Enabled está en true pero falta Turnstile:SiteKey: Turnstile queda APAGADO.");
        else
            _log.LogInformation("Turnstile activo en el acceso (site key {SiteKey}).", SiteKey);
        if (Activo && _exentas.Count > 0)
            _log.LogInformation("Turnstile no se exige a las peticiones directas sin token desde {Redes} (Turnstile:ExemptNetworks).",
                string.Join(", ", _exentas));
    }

    // La site key para el front: null si está apagado o si la petición viene de una red de
    // confianza de la instancia (ahí no se pide).
    public string? SiteKeyPara(HttpContext ctx)
        => Activo && !RedesConfianza.EnInstancia(ClientIp.Of(ctx)) ? SiteKey : null;

    public async Task<Resultado> ValidarAsync(HttpContext ctx, string? token)
    {
        if (!Activo) return Resultado.Ok;
        var ip = ClientIp.Of(ctx);
        if (RedesConfianza.EnInstancia(ip)) return Resultado.Ok;
        token = token?.Trim();
        // Sin token y directa desde Turnstile:ExemptNetworks (seeds en el servidor, front de
        // desarrollo sin widget): no se exige. Con token se valida como cualquier otra.
        if (string.IsNullOrEmpty(token) && DirectaDesdeExenta(ctx)) return Resultado.Ok;
        if (string.IsNullOrEmpty(token) || token.Length > 2048) return Resultado.Invalido;

        var campos = new Dictionary<string, string> { ["secret"] = SecretKey!, ["response"] = token };
        if (System.Net.IPAddress.TryParse(ip, out _)) campos["remoteip"] = ip;
        try
        {
            using var contenido = new FormUrlEncodedContent(campos);
            using var resp = await _http.CreateClient(ClienteHttp).PostAsync(UrlVerificar, contenido);
            // Cloudflare responde con JSON también en los 4xx (p. ej. 400 con la clave secreta
            // mala): se leen los códigos de error en todos los casos.
            var cuerpo = await resp.Content.ReadAsStringAsync();
            System.Text.Json.JsonDocument doc;
            try { doc = System.Text.Json.JsonDocument.Parse(cuerpo); }
            catch (System.Text.Json.JsonException)
            {
                _log.LogError("Turnstile: Cloudflare respondió {Status} sin JSON al validar el token.", (int)resp.StatusCode);
                return Resultado.NoDisponible;
            }
            using var _ = doc;
            var raiz = doc.RootElement;
            if (resp.IsSuccessStatusCode && raiz.ValueKind == System.Text.Json.JsonValueKind.Object
                && raiz.TryGetProperty("success", out var ok) && ok.ValueKind == System.Text.Json.JsonValueKind.True)
                return Resultado.Ok;

            var codigos = raiz.ValueKind == System.Text.Json.JsonValueKind.Object
                && raiz.TryGetProperty("error-codes", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Array
                ? e.EnumerateArray().Select(x => x.ValueKind == System.Text.Json.JsonValueKind.String ? x.GetString() ?? "" : "").ToList()
                : new List<string>();
            // Solo es culpa de quien entra si Cloudflare rechazó el token; lo demás (clave
            // secreta mala, error interno, respuesta rara) es nuestro o de Cloudflare.
            var tokenMalo = codigos.Count > 0 && codigos.All(c => c is "missing-input-response" or "invalid-input-response"
                or "timeout-or-duplicate" or "bad-request");
            if (!tokenMalo)
            {
                _log.LogError("Turnstile: Cloudflare no pudo validar ({Status}: {Codigos}). Revisa Turnstile:SecretKey.",
                    (int)resp.StatusCode, string.Join(", ", codigos));
                return Resultado.NoDisponible;
            }
            _log.LogInformation("Turnstile: token rechazado ({Codigos}) desde {Ip}.", string.Join(", ", codigos), ip);
            return Resultado.Invalido;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Turnstile: no se pudo validar con Cloudflare (sin respuesta en 5 s o error de red).");
            return Resultado.NoDisponible;
        }
    }

    // Token ausente o inválido: 400 genérico (no dice nada de la cuenta). Cloudflare no
    // responde: 503 claro.
    public static IResult Respuesta(Resultado r) => r == Resultado.Invalido
        ? Results.BadRequest(new { error = "No pudimos confirmar la verificación de seguridad. Vuelve a intentarlo.", turnstileFailed = true })
        : Results.Json(new { error = "No pudimos comprobar la verificación de seguridad en este momento. Intenta de nuevo en unos minutos.", turnstileFailed = true },
            statusCode: StatusCodes.Status503ServiceUnavailable);
}

// «Perdí mi autenticador»: recuperar el acceso con un código al correo validado.
public static class Recuperacion
{
    public const string Proposito = "recover-2fa";
    public const int MaxDia = 3;

    // null = la persona puede recuperar por correo; si no, el motivo (solo para la auditoría:
    // la respuesta al cliente es genérica).
    public static async Task<string?> MotivoRechazoAsync(CatalogDbContext c, AppUser u)
    {
        if (u.TenantId is null && u.Role == "Admin") return "admin de plataforma";
        if (u.EmailVerifiedAt is null) return "correo sin validar";
        var ids = (await Compañias.DeUsuarioAsync(c, u)).Select(x => x.TenantId).ToList();
        if (ids.Count > 0)
        {
            var jsons = await c.Tenants.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => t.SecurityConfigJson).ToListAsync();
            if (jsons.Any(j => !SecurityConfig.Parse(j).EmailRecoveryEnabled)) return "la compañía desactivó la recuperación por correo";
        }
        return null;
    }

    // 3 códigos de recuperación al día por cuenta, además de los topes generales de los
    // códigos por correo (60 s entre envíos, 10 al día).
    public static Task<TimeSpan?> ReservarEnvioAsync(CatalogDbContext c, Guid userId)
        => Bloqueos.ReservarEnvioCodigoAsync(c, userId, EventosSeguridad.RecuperacionEnviada, MaxDia);
}

// Prueba de que quien tiene la sesión también tiene el correo (un código de alta del
// autenticador o de recuperación canjeado). La primera alta del autenticador desde fuera
// de las redes de confianza la exige: vale 15 minutos y solo desde la misma IP.
public static class PruebaCorreo
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(15);
    public const string Proposito = "enroll-2fa";

    public static void Anotar(CatalogDbContext c, Guid userId, string? ip)
        => EventosSeguridad.Anotar(c, EventosSeguridad.PruebaCorreo, userId, ip: ip);

    public static Task<bool> VigenteAsync(CatalogDbContext c, Guid userId, string? ip)
    {
        var desde = DateTime.UtcNow - Vigencia;
        return c.SecurityEvents.AnyAsync(e => e.Kind == EventosSeguridad.PruebaCorreo && e.UserId == userId
                                              && e.At > desde && e.Ip == ip);
    }

    // ¿La primera alta del autenticador necesita antes un código por correo?
    public static async Task<bool> HaceFaltaAsync(CatalogDbContext c, AppUser u, string? ip)
    {
        if (u.TwoFactorMode == "totp" && u.TwoFactorConfirmedAt is not null) return false;   // es un cambio: pide clave y código actual
        if (await RedesConfianza.ParaUsuarioAsync(c, u, ip)) return false;
        return !await VigenteAsync(c, u.Id, ip);
    }
}

// Quitar el autenticador de una cuenta (recuperación por correo o reinicio del admin):
// sin secreto, sin pendiente, contadores a 0 y sello nuevo (cierra todas sus sesiones).
// Se guarda con el próximo SaveChanges de quien llama.
public static class DobleFactor
{
    public static bool Tiene(AppUser u) => u.TwoFactorMode != "none" || u.TotpSecret is not null || u.PendingTotpSecret is not null;

    public static void Quitar(CatalogDbContext c, AppUser u, IMemoryCache cache)
    {
        u.TwoFactorMode = "none";
        u.TotpSecret = null;
        u.PendingTotpSecret = null;
        u.TwoFactorConfirmedAt = null;
        u.LastTotpStep = null;
        u.TwoFactorFailedCount = 0;
        u.TwoFactorLockedUntil = null;
        Sesiones.Rotar(c, u, cache);
    }
}

// Avisos por correo a los Admin de las compañías de una persona (las que tienen
// notifyAdmins), sin incluirla a ella. soloFueraDeConfianza: se salta la compañía si la IP
// está en sus redes de confianza o en las de la instancia.
public static class AvisosSeguridad
{
    public static async Task AdminsAsync(CatalogDbContext c, IEmailSender email, ILogger log, AppUser u,
        string evento, string? ip, bool soloFueraDeConfianza = false)
    {
        var ids = (await Compañias.DeUsuarioAsync(c, u)).Select(x => x.TenantId).ToList();
        if (ids.Count == 0) return;
        var compañias = await c.Tenants.AsNoTracking().Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.SecurityConfigJson }).ToListAsync();
        var asunto = evento == "2fa-recovered"
            ? "Aviso de seguridad: un usuario recuperó su acceso sin su app autenticadora"
            : "Aviso de seguridad: un usuario restableció su contraseña";
        var avisados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in compañias)
        {
            var sc = SecurityConfig.Parse(t.SecurityConfigJson);
            if (!sc.NotifyAdmins) continue;
            if (soloFueraDeConfianza && (RedesConfianza.EnInstancia(ip) || RedesConfianza.En(sc.Redes(), ip))) continue;
            foreach (var a in (await CompanyUsers.OfAsync(c, t.Id)).Where(m => m.Role == "Admin" && m.Id != u.Id))
            {
                if (!avisados.Add(a.Email)) continue;
                try
                {
                    await email.SendAsync(a.Email, a.Name, asunto,
                        EmailTemplates.AdminSecurityNotice(a.Name, u.Name, u.Email, t.Name, evento, DateTime.UtcNow, ip));
                }
                catch (Exception ex) { log.LogWarning(ex, "No se pudo avisar a {Admin} de {Evento} de {Email}", a.Email, evento, u.Email); }
            }
        }
    }
}

// Configuración de seguridad de una compañía: GET/PUT /company/security (su Admin) y
// /admin/tenants/{id}/security (admin de plataforma).
public sealed record SecurityConfigRequest(List<string?>? TrustedNetworks, bool? EmailRecoveryEnabled, bool? NotifyAdmins);

public static class SeguridadCompañia
{
    // conInstancia: el admin de plataforma ve también las redes de la instancia.
    public static object Vista(Tenant t, string ip, bool conInstancia)
    {
        var sc = SecurityConfig.Parse(t.SecurityConfigJson);
        return new
        {
            tenantId = t.Id,
            name = t.Name,
            twoFactorPolicy = t.TwoFactorPolicy,          // se edita en /admin/tenants/{id}/two-factor
            trustedNetworks = sc.TrustedNetworks,
            emailRecoveryEnabled = sc.EmailRecoveryEnabled,
            notifyAdmins = sc.NotifyAdmins,
            yourIp = ip,
            yourIpTrusted = RedesConfianza.EnInstancia(ip) || RedesConfianza.En(sc.Redes(), ip),
            instanceTrustedNetworks = conInstancia ? RedesConfianza.Instancia.Select(r => r.ToString()).ToList() : null
        };
    }

    // Cambio parcial: lo que venga en null se queda como está. Cada red se valida; si alguna
    // no sirve, 400 con la lista y no se guarda nada.
    public static async Task<IResult> GuardarAsync(CatalogDbContext c, Tenant t, SecurityConfigRequest? req,
        Guid? quien, string ip, bool conInstancia)
    {
        if (req is null) return Results.BadRequest("Faltan los datos.");
        var sc = SecurityConfig.Parse(t.SecurityConfigJson);
        if (req.TrustedNetworks is not null)
        {
            var escritas = req.TrustedNetworks.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (escritas.Count > SecurityConfig.MaxRedes)
                return Results.BadRequest(new { error = $"Como máximo {SecurityConfig.MaxRedes} redes de confianza." });
            var redes = SecurityConfig.Normalizar(escritas, out var invalidas);
            if (invalidas.Count > 0)
            {
                var muestra = invalidas.Take(10).Select(s => s.Length > 64 ? s[..64] : s).ToList();
                return Results.BadRequest(new
                {
                    error = $"Estas redes no son válidas: {string.Join(", ", muestra)}. Escribe una IP (203.0.113.7) o una red en " +
                            $"formato CIDR (203.0.113.0/24). No se admiten redes más amplias que /{Cidr.MinPrefijoV4} en IPv4 " +
                            $"ni que /{Cidr.MinPrefijoV6} en IPv6.",
                    invalidNetworks = muestra
                });
            }
            sc.TrustedNetworks = redes;
        }
        if (req.EmailRecoveryEnabled is bool rec) sc.EmailRecoveryEnabled = rec;
        if (req.NotifyAdmins is bool avisos) sc.NotifyAdmins = avisos;

        t.SecurityConfigJson = sc.ToJson();
        c.AuditLogs.Add(new CatalogAuditLog
        {
            Action = "security-config",
            Detail = $"{t.Name}: redes [{string.Join(", ", sc.TrustedNetworks)}], recuperación por correo " +
                     $"{(sc.EmailRecoveryEnabled ? "sí" : "no")}, avisos a admins {(sc.NotifyAdmins ? "sí" : "no")}",
            UserId = quien
        });
        await c.SaveChangesAsync();
        return Results.Ok(Vista(t, ip, conInstancia));
    }
}
