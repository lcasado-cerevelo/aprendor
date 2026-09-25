namespace TrainingPlatform;

// Bitácora mínima a un archivo de texto plano, sin depender de cómo esté hospedada
// la app (IIS, servicio, consola) ni de configurar nada aparte. Solo Warning y más
// grave — no es para trazar cada request, es para ver errores reales rápido.
//
// Rotación diaria: a partir de la ruta base (App_Data\logs\app-log.txt) escribe en
// app-log-AAAAMMDD.txt y conserva los últimos MaxArchivos días; los más viejos se
// borran al cambiar de día (y al arrancar).
public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    public const int MaxArchivos = 14;

    private readonly string _carpeta, _prefijo, _extension;
    private readonly object _lock = new();
    private readonly bool _disponible;
    private DateTime _dia = DateTime.MinValue;
    private string _archivo = "";

    // Nunca debe poder tumbar el arranque de la app: si no se puede crear la
    // carpeta (permisos del identity de IIS, disco, lo que sea), el logger
    // simplemente no escribe nada — no lanza.
    public SimpleFileLoggerProvider(string path)
    {
        _carpeta = Path.GetDirectoryName(path)!;
        _prefijo = Path.GetFileNameWithoutExtension(path);
        _extension = Path.GetExtension(path);
        try { Directory.CreateDirectory(_carpeta); _disponible = true; }
        catch { _disponible = false; }
    }

    public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(categoryName, this);

    public void Dispose() { }

    private void Escribir(string linea)
    {
        try
        {
            lock (_lock)
            {
                var hoy = DateTime.Now.Date;
                if (hoy != _dia)
                {
                    _dia = hoy;
                    _archivo = Path.Combine(_carpeta, $"{_prefijo}-{hoy:yyyyMMdd}{_extension}");
                    Depurar();
                }
                File.AppendAllText(_archivo, linea + Environment.NewLine);
            }
        }
        catch { /* nunca debe tumbar la app por no poder escribir el log */ }
    }

    // Deja solo los MaxArchivos más recientes (el nombre lleva la fecha, así que el
    // orden alfabético es el cronológico).
    private void Depurar()
    {
        try
        {
            var viejos = Directory.GetFiles(_carpeta, $"{_prefijo}-????????{_extension}")
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .Skip(MaxArchivos);
            foreach (var f in viejos) { try { File.Delete(f); } catch { } }
        }
        catch { }
    }

    private sealed class SimpleFileLogger : ILogger
    {
        private readonly string _category;
        private readonly SimpleFileLoggerProvider _p;

        public SimpleFileLogger(string category, SimpleFileLoggerProvider p)
        {
            _category = category;
            _p = p;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _p._disponible && logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_category}: {formatter(state, exception)}";
            if (exception is not null) linea += Environment.NewLine + exception;
            _p.Escribir(linea);
        }
    }
}
