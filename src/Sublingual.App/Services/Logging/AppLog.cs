using Microsoft.Extensions.Logging;

namespace Sublingual.App.Services.Logging;

public static class AppLog
{
    public const string Category = "Sublingual";

    public static ILoggerFactory Factory { get; private set; } = LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(LogLevel.Information)
            .AddDebug();
    });

    public static ILogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);

    public static void Initialize(ILoggerFactory loggerFactory)
    {
        Factory = loggerFactory;
    }
}
