using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sublingual.App.Services;
using Sublingual.App.ViewModels;
using Sublingual.App.Views;
using Sublingual.Desktop;

namespace Sublingual.App;

public partial class App : Avalonia.Application
{
    private AppBootstrapper? _bootstrapper;
    private OverlayWindow? _overlayWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _bootstrapper = new AppBootstrapper();
            var mainWindow = _bootstrapper.CreateMainWindow();
            _overlayWindow = _bootstrapper.CreateOverlayWindow();

            // Wire toggle action from MainWindowViewModel → actual window show/hide
            if (mainWindow.DataContext is MainWindowViewModel mainVm &&
                _overlayWindow.DataContext is OverlayWindowViewModel overlayVm)
            {
                _overlayWindow.OverlayHidden += (_, _) =>
                {
                    mainVm.IsOverlayVisible = false;
                };

                mainVm.ToggleOverlayAction = () =>
                {
                    if (_overlayWindow is null)
                    {
                        return;
                    }

                    if (_overlayWindow.IsVisible)
                    {
                        _overlayWindow.Hide();
                        mainVm.IsOverlayVisible = false;
                    }
                    else
                    {
                        // Sync size + font from main settings to overlay VM
                        overlayVm.OverlayFontSize = mainVm.OverlayFontSize;
                        overlayVm.OverlayWidth = mainVm.OverlayWidth;
                        overlayVm.OverlayHeight = mainVm.OverlayHeight;
                        overlayVm.OverlayTheme = mainVm.OverlayTheme;
                        overlayVm.OverlayOpacity = mainVm.OverlayOpacity;
                        _overlayWindow.Width = mainVm.OverlayWidth;
                        _overlayWindow.Height = mainVm.OverlayHeight;
                        if (!_overlayWindow.IsVisible)
                        {
                            _overlayWindow.Show();
                        }

                        mainVm.IsOverlayVisible = true;
                    }
                };

                // Keep overlay size in sync as sliders change
                mainVm.PropertyChanged += (_, e) =>
                {
                    if (_overlayWindow is null) return;
                    if (e.PropertyName == nameof(MainWindowViewModel.OverlayFontSize))
                        overlayVm.OverlayFontSize = mainVm.OverlayFontSize;
                    if (e.PropertyName == nameof(MainWindowViewModel.OverlayTheme))
                        overlayVm.OverlayTheme = mainVm.OverlayTheme;
                    if (e.PropertyName == nameof(MainWindowViewModel.OverlayOpacity))
                        overlayVm.OverlayOpacity = mainVm.OverlayOpacity;
                    if (e.PropertyName == nameof(MainWindowViewModel.OverlayWidth))
                    {
                        overlayVm.OverlayWidth = mainVm.OverlayWidth;
                        if (_overlayWindow.IsVisible)
                            _overlayWindow.Width = mainVm.OverlayWidth;
                    }
                    if (e.PropertyName == nameof(MainWindowViewModel.OverlayHeight))
                    {
                        overlayVm.OverlayHeight = mainVm.OverlayHeight;
                        if (_overlayWindow.IsVisible)
                            _overlayWindow.Height = mainVm.OverlayHeight;
                    }
                };
            }

            desktop.MainWindow = mainWindow;
            // Overlay is NOT shown on startup — opened via Overlay tab toggle
            desktop.Exit += OnDesktopExit;

            if (ShouldRunDebugCapture())
                _ = MacOsDebugCaptureRunner.RunAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _overlayWindow?.Close();
        _overlayWindow = null;
        _bootstrapper?.Dispose();
        _bootstrapper = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Exit -= OnDesktopExit;
    }

    private static bool ShouldRunDebugCapture()
    {
        var env = Environment.GetEnvironmentVariable("SUBLINGUAL_DEBUG_CAPTURE");
        if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
            return true;

        return Program.LaunchArgs.Any(arg =>
            string.Equals(arg, "--debug-capture", StringComparison.OrdinalIgnoreCase));
    }
}
