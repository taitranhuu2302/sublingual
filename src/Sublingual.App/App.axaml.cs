using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System;
using Avalonia.Markup.Xaml;
using Sublingual.App.ViewModels;
using Sublingual.App.Views;
using Sublingual.Desktop;

namespace Sublingual.App;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            if (ShouldRunDebugCapture())
            {
                _ = MacOsDebugCaptureRunner.RunAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool ShouldRunDebugCapture()
    {
        var environmentValue = Environment.GetEnvironmentVariable("SUBLINGUAL_DEBUG_CAPTURE");
        if (string.Equals(environmentValue, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Program.LaunchArgs.Any(arg =>
            string.Equals(arg, "--debug-capture", StringComparison.OrdinalIgnoreCase)
        );
    }
}
