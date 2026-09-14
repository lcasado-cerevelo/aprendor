namespace TrainingPlatform;

// Bitácora mínima a un archivo de texto plano, sin depender de cómo esté hospedada
// la app (IIS, servicio, consola) ni de configurar nada aparte. Solo Warning y más
// grave — no es para trazar cada request, es para ver errores reales rápido.
public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly bool _disponible;

    // Nunca debe poder tumbar el arranque de la app: si no se puede crear la
    // carpeta (permisos del identity de IIS, disco, lo que sea), el logger
    // simplemente no escribe nada — no lanza.
    public SimpleFileLoggerProvider(string path)
    {
        _path = path;
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); _disponible = true; }
        catch { _disponible = false; }
    }

    public ILogger CreateLogger(string categoryName) => new SimpleFileLogger(categoryName, _path, _lock, _disponible);

    public void Dispose() { }

    private sealed class SimpleFileLogger : ILogger
    {
        private readonly string _category, _path;
        private readonly object _lock;
        private readonly bool _disponible;

        public SimpleFileLogger(string category, string path, object l, bool disponible)
        {
            _category = category;
            _path = path;
            _lock = l;
            _disponible = disponible;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _disponible && logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_category}: {formatter(state, exception)}";
            if (exception is not null) linea += Environment.NewLine + exception;
            try { lock (_lock) { File.AppendAllText(_path, linea + Environment.NewLine); } }
            catch { /* nunca debe tumbar la app por no poder escribir el log */ }
        }
    }
}
