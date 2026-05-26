using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Sublingual.App.Services.Logging;

/// <summary>
/// Minimal file logger for desktop app diagnostics.
/// Writes line-delimited text logs and rolls daily.
/// </summary>
public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minLevel;
    private readonly ConcurrentDictionary<string, SimpleFileLogger> _loggers = new(StringComparer.Ordinal);

    public SimpleFileLoggerProvider(string directory, LogLevel minLevel)
    {
        _directory = directory;
        _minLevel = minLevel;
        Directory.CreateDirectory(_directory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new SimpleFileLogger(name, _directory, _minLevel));
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }

        _loggers.Clear();
    }
}

internal sealed class SimpleFileLogger : ILogger, IDisposable
{
    private readonly string _category;
    private readonly string _directory;
    private readonly LogLevel _minLevel;
    private readonly Lock _gate = new();
    private DateOnly _currentDate;
    private StreamWriter? _writer;

    public SimpleFileLogger(string category, string directory, LogLevel minLevel)
    {
        _category = category;
        _directory = directory;
        _minLevel = minLevel;
        _currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(utcNow.UtcDateTime);
        var line = FormatLine(utcNow, logLevel, eventId, message, exception);

        lock (_gate)
        {
            EnsureWriter(date);
            _writer!.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void EnsureWriter(DateOnly date)
    {
        if (_writer is not null && date == _currentDate)
        {
            return;
        }

        _writer?.Dispose();
        _writer = null;
        _currentDate = date;

        var filePath = Path.Combine(_directory, $"app-{date:yyyy-MM-dd}.log");
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream)
        {
            AutoFlush = true,
            NewLine = "\n",
        };
    }

    private string FormatLine(DateTimeOffset utcNow, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        // UTC timestamp for easier cross-machine correlation.
        var ts = utcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var lvl = level.ToString();
        var evt = eventId.Id == 0 && string.IsNullOrWhiteSpace(eventId.Name)
            ? string.Empty
            : $" [{eventId.Id}:{eventId.Name}]";

        if (exception is null)
        {
            return $"{ts} {lvl} {_category}{evt} {message}";
        }

        return $"{ts} {lvl} {_category}{evt} {message} | {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
