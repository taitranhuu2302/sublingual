using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.App.ViewModels;
using Sublingual.App.Views;
using Sublingual.Desktop;

namespace Sublingual.App;

public partial class App : Avalonia.Application
{
    private AppBootstrapper? _bootstrapper;
    private OverlayWindow? _overlayWindow;
    private AppSettingsStore? _settingsStore;
    private AppSettings? _settings;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _bootstrapper = new AppBootstrapper();
            _settingsStore = _bootstrapper.Services.GetRequiredService<AppSettingsStore>();
            _settings = _settingsStore.Load();

            var mainWindow = _bootstrapper.CreateMainWindow();
            _overlayWindow = _bootstrapper.CreateOverlayWindow();

            if (mainWindow.DataContext is MainWindowViewModel mainVm &&
                _overlayWindow.DataContext is OverlayWindowViewModel overlayVm)
            {
                ApplyOverlaySettingsToMainViewModel(mainVm, _settings.Overlay);
                ApplyOverlaySettingsToOverlay(mainVm, overlayVm, _overlayWindow, _settings.Overlay);

                _overlayWindow.PositionChanged += (_, _) => SaveOverlayPosition(_overlayWindow, mainVm);
                _overlayWindow.OverlayHidden += (_, _) =>
                {
                    mainVm.IsOverlayVisible = false;
                    SaveOverlayPosition(_overlayWindow, mainVm);
                };

                mainVm.ToggleOverlayAction = () =>
                {
                    if (_overlayWindow is null)
                    {
                        return;
                    }

                    if (_overlayWindow.IsVisible)
                    {
                        SaveOverlayPosition(_overlayWindow, mainVm);
                        _overlayWindow.Hide();
                        mainVm.IsOverlayVisible = false;
                    }
                    else
                    {
                        ApplyOverlaySettingsToOverlay(mainVm, overlayVm, _overlayWindow, _settings.Overlay);
                        if (!_overlayWindow.IsVisible)
                        {
                            _overlayWindow.Show();
                        }

                        mainVm.IsOverlayVisible = true;
                    }
                };

                mainVm.PropertyChanged += (_, e) =>
                {
                    if (_overlayWindow is null)
                    {
                        return;
                    }

                    if (e.PropertyName is nameof(MainWindowViewModel.OverlayFontSize)
                        or nameof(MainWindowViewModel.OverlayTheme)
                        or nameof(MainWindowViewModel.OverlayOpacity)
                        or nameof(MainWindowViewModel.OverlayWidth)
                        or nameof(MainWindowViewModel.OverlayHeight))
                    {
                        ApplyOverlaySettingsToOverlay(mainVm, overlayVm, _overlayWindow, _settings!.Overlay);
                        SaveOverlaySettings(mainVm);
                    }
                };
            }

            desktop.MainWindow = mainWindow;
            desktop.Exit += OnDesktopExit;

            if (ShouldRunDebugCapture())
            {
                _ = MacOsDebugCaptureRunner.RunAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_overlayWindow?.DataContext is OverlayWindowViewModel overlayVm &&
            ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainWindowViewModel mainVm)
        {
            SaveOverlayPosition(_overlayWindow, mainVm);
            SaveOverlaySettings(mainVm);
        }

        _overlayWindow?.Close();
        _overlayWindow = null;
        _bootstrapper?.Dispose();
        _bootstrapper = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Exit -= OnDesktopExit;
        }
    }

    private void ApplyOverlaySettingsToMainViewModel(MainWindowViewModel mainVm, OverlaySettings settings)
    {
        mainVm.OverlayFontSize = settings.FontSize;
        mainVm.OverlayWidth = settings.Width;
        mainVm.OverlayHeight = settings.Height;
        mainVm.OverlayTheme = string.IsNullOrWhiteSpace(settings.Theme) ? "Dark" : settings.Theme;
        mainVm.OverlayOpacity = settings.Opacity <= 0 ? 0.88 : settings.Opacity;
    }

    private static void ApplyOverlaySettingsToOverlay(
        MainWindowViewModel mainVm,
        OverlayWindowViewModel overlayVm,
        OverlayWindow overlayWindow,
        OverlaySettings settings)
    {
        overlayVm.OverlayFontSize = mainVm.OverlayFontSize;
        overlayVm.OverlayWidth = mainVm.OverlayWidth;
        overlayVm.OverlayHeight = mainVm.OverlayHeight;
        overlayVm.OverlayTheme = mainVm.OverlayTheme;
        overlayVm.OverlayOpacity = mainVm.OverlayOpacity;

        overlayWindow.Width = mainVm.OverlayWidth;
        overlayWindow.Height = mainVm.OverlayHeight;

        if (settings.PositionX.HasValue && settings.PositionY.HasValue)
        {
            overlayWindow.Position = new PixelPoint(settings.PositionX.Value, settings.PositionY.Value);
        }
    }

    private void SaveOverlaySettings(MainWindowViewModel mainVm)
    {
        if (_settingsStore is null || _settings is null)
        {
            return;
        }

        _settings.Overlay.FontSize = mainVm.OverlayFontSize;
        _settings.Overlay.Width = mainVm.OverlayWidth;
        _settings.Overlay.Height = mainVm.OverlayHeight;
        _settings.Overlay.Theme = mainVm.OverlayTheme;
        _settings.Overlay.Opacity = mainVm.OverlayOpacity;
        _settingsStore.Save(_settings);
    }

    private void SaveOverlayPosition(OverlayWindow overlayWindow, MainWindowViewModel mainVm)
    {
        if (_settingsStore is null || _settings is null)
        {
            return;
        }

        _settings.Overlay.PositionX = overlayWindow.Position.X;
        _settings.Overlay.PositionY = overlayWindow.Position.Y;
        SaveOverlaySettings(mainVm);
    }

    private static bool ShouldRunDebugCapture()
    {
        var env = Environment.GetEnvironmentVariable("SUBLINGUAL_DEBUG_CAPTURE");
        if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Program.LaunchArgs.Any(arg =>
            string.Equals(arg, "--debug-capture", StringComparison.OrdinalIgnoreCase));
    }
}
