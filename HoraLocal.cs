using System.Globalization;

namespace TrainingPlatform;

// ============================================================================
//  Hora de la aplicación (App:TimeZone)
//  ------------------------------------
//  Todo se guarda en UTC; lo que se MUESTRA (PDF del certificado, correos, textos del
//  servidor) y los días del calendario (hoy, fechas límite, días que faltan) se cuentan
//  en la zona de la aplicación, por defecto America/Puerto_Rico, sin importar la zona de
//  la máquina (el servidor en la nube puede estar en UTC). El front usa la misma zona
//  (index.html, certificate.html). Nada de ToLocalTime ni DateTime.Now para mostrar.
//
//  Zona: App:TimeZone (IANA o Windows; .NET 8 acepta los dos en cualquier sistema). Si no
//  se encuentra, respaldo «SA Western Standard Time» (UTC-4 sin horario de verano, la de
//  Puerto Rico en Windows); si tampoco, UTC-4 fija.
// ============================================================================
public static class HoraLocal
{
    public const string ZonaPorDefecto = "America/Puerto_Rico";
    private const string Respaldo = "SA Western Standard Time";

    private static TimeZoneInfo _zona = Resolver(null, out _);
    private static string _etiqueta = "hora de Puerto Rico";

    public static TimeZoneInfo Zona => _zona;

    // «hora de Puerto Rico» (o el id de la zona configurada), para los textos con hora.
    public static string Etiqueta => _etiqueta;

    // Se llama al arrancar con App:TimeZone. Devuelve un aviso si hubo que usar un respaldo.
    public static string? Configurar(string? id)
    {
        var pedido = string.IsNullOrWhiteSpace(id) ? ZonaPorDefecto : id.Trim();
        _zona = Resolver(pedido, out var aviso);
        _etiqueta = EsPuertoRico(pedido) || aviso is not null ? "hora de Puerto Rico" : "hora " + pedido;
        return aviso;
    }

    private static bool EsPuertoRico(string id)
        => id.Equals(ZonaPorDefecto, StringComparison.OrdinalIgnoreCase)
           || id.Equals(Respaldo, StringComparison.OrdinalIgnoreCase);

    private static TimeZoneInfo Resolver(string? id, out string? aviso)
    {
        aviso = null;
        var pedido = string.IsNullOrWhiteSpace(id) ? ZonaPorDefecto : id.Trim();
        try { return TimeZoneInfo.FindSystemTimeZoneById(pedido); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        try
        {
            var z = TimeZoneInfo.FindSystemTimeZoneById(Respaldo);
            aviso = $"App:TimeZone: no se encontró la zona «{pedido}»; se usa «{Respaldo}» (UTC-4, Puerto Rico).";
            return z;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        aviso = $"App:TimeZone: no se encontró la zona «{pedido}» ni «{Respaldo}»; se usa UTC-4 fija.";
        return TimeZoneInfo.CreateCustomTimeZone("UTC-04", TimeSpan.FromHours(-4), "UTC-04:00", "UTC-04:00");
    }

    // Un instante UTC (EF lo devuelve sin Kind: se toma como UTC) en la hora de la aplicación.
    public static DateTime De(DateTime utc)
    {
        var u = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(u, _zona), DateTimeKind.Unspecified);
    }

    public static DateTime Ahora() => De(DateTime.UtcNow);

    // El día de hoy en la hora de la aplicación (solo la fecha).
    public static DateTime Hoy() => Ahora().Date;

    // El día del calendario (en la hora de la aplicación) de un instante UTC.
    public static DateTime DiaDe(DateTime utc) => De(utc).Date;

    // Días del calendario que faltan desde hoy hasta ese instante (negativo si ya pasó).
    public static int DiasHasta(DateTime utc) => (DiaDe(utc) - Hoy()).Days;

    // Una hora del reloj de la aplicación (sin zona) pasada a UTC. En un salto de horario de
    // verano (hora que no existe) se corre una hora; Puerto Rico no tiene horario de verano.
    public static DateTime AUtc(DateTime local)
    {
        var l = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (_zona.IsInvalidTime(l)) l = l.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(l, _zona);
    }

    // Fecha límite de un día: el final (23:59:59) de ese día en la hora de la aplicación, en UTC.
    public static DateTime FinDelDia(DateTime dia) => AUtc(dia.Date.AddDays(1).AddSeconds(-1));

    // Comienzo (00:00) de ese día en la hora de la aplicación, en UTC.
    public static DateTime InicioDelDia(DateTime dia) => AUtc(dia.Date);

    // Formatos de los textos del servidor (correos, auditoría).
    public static string Fecha(DateTime utc) => De(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FechaHora(DateTime utc)
        => De(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (" + _etiqueta + ")";

    // Sello de un día (yyyyMMdd, en la hora de la aplicación) para las claves de idempotencia de los avisos.
    public static string Sello(DateTime utc) => De(utc).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
