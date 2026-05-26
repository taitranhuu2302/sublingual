using Microsoft.Extensions.Logging;

namespace Sublingual.App.Services.Logging;

public static class LoggingBootstrapper
{
    public static ILoggerFactory CreateLoggerFactory(string logDirectory)
    {
        var minLevel = LogLevel.Information;
        if (string.Equals(Environment.GetEnvironmentVariable("SUBLINGUAL_LOG_LEVEL"), "debug", StringComparison.OrdinalIgnoreCase))
        {
            minLevel = LogLevel.Debug;
        }

        return LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(minLevel)
                .AddDebug()
                .AddConsole();

            builder.AddProvider(new SimpleFileLoggerProvider(logDirectory, minLevel));
        });
    }

    public static string ResolveLogDirectory()
    {
        // Default: ~/.sublingual/logs
        var appRoot = AppPathHelper.GetDefaultAppRoot();
        return Path.Combine(appRoot, "logs");
    }
}
