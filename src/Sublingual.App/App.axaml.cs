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
using SukiUI.MessageBox;

namespace Sublingual.App;

public partial class App : Avalonia.Application
{
    private AppBootstrapper? _bootstrapper;
    private MainWindow? _mainWindow;
    private OverlayWindow? _overlayWindow;
    private AppSettingsStore? _settingsStore;
    private AppSettings? _settings;
    private TrayIcon? _trayIcon;
    private bool _isExitRequested;
    private bool _isShowingTrayExitHint;

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
            _mainWindow = mainWindow;
            _overlayWindow = _bootstrapper.CreateOverlayWindow();
            ConfigureTrayIcon();

            mainWindow.Closing += OnMainWindowClosing;

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

                mainVm.EnsureOverlayVisibleAction = () =>
                {
                    if (_overlayWindow is null || _overlayWindow.IsVisible)
                    {
                        return;
                    }

                    ApplyOverlaySettingsToOverlay(mainVm, overlayVm, _overlayWindow, _settings.Overlay);
                    _overlayWindow.Show();
                    mainVm.IsOverlayVisible = true;
                };

                mainVm.PropertyChanged += (_, e) =>
                {
                    if (_overlayWindow is null)
                    {
                        return;
                    }

                    if (e.PropertyName is nameof(MainWindowViewModel.OverlayFontSize)
                        or nameof(MainWindowViewModel.OverlayTheme)
                        or nameof(MainWindowViewModel.OverlayOpacity))
                    {
                        ApplyOverlaySettingsToOverlay(mainVm, overlayVm, _overlayWindow, _settings!.Overlay);
                        SaveOverlaySettings(mainVm);
                    }
                };
            }

            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += OnDesktopShutdownRequested;
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
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            _mainWindow = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;

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
            lifetime.ShutdownRequested -= OnDesktopShutdownRequested;
            lifetime.Exit -= OnDesktopExit;
        }
    }

    private void OnDesktopShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        _isExitRequested = true;
    }

    private void ConfigureTrayIcon()
    {
        _trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Clicked += OnTrayIconClicked;

        if (_trayIcon.Menu?.Items is { Count: >= 2 } items)
        {
            if (items[0] is NativeMenuItem openItem)
            {
                openItem.Click += OnTrayOpenClicked;
            }

            if (items[1] is NativeMenuItemSeparator && items.Count >= 3 && items[2] is NativeMenuItem exitItem)
            {
                exitItem.Click += OnTrayExitClicked;
            }
        }
    }

    private async void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExitRequested || sender is not Window window)
        {
            return;
        }

        e.Cancel = true;

        if (_isShowingTrayExitHint)
        {
            return;
        }

        if (!(_settings?.Ui.HasSeenTrayExitHint ?? false))
        {
            _isShowingTrayExitHint = true;

            try
            {
                await SukiMessageBox.ShowDialogResult(
                    window,
                    "The app will keep running in the system tray so you can reopen it quickly. To quit completely, right-click the tray icon and choose Exit.",
                    SukiMessageBoxButtons.OK,
                    "Minimized To Tray",
                    "Sublingual stays active in tray",
                    SukiMessageBoxIcons.Information,
                    null);

                if (_settingsStore is not null)
                {
                    _settings ??= _settingsStore.Load();
                    _settings.Ui.HasSeenTrayExitHint = true;
                    _settingsStore.Save(_settings);
                }
            }
            finally
            {
                _isShowingTrayExitHint = false;
            }
        }

        window.Hide();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnTrayOpenClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnTrayExitClicked(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        _isExitRequested = true;
        desktop.Shutdown();
    }

    private void ApplyOverlaySettingsToMainViewModel(MainWindowViewModel mainVm, OverlaySettings settings)
    {
        mainVm.OverlayFontSize = settings.FontSize;
        mainVm.OverlayLineHeight = settings.LineHeight <= 0 ? 1.35 : settings.LineHeight;
        mainVm.OverlayWidth = settings.Width;
        mainVm.OverlayHeight = settings.Height;
        mainVm.OverlayTheme = string.IsNullOrWhiteSpace(settings.Theme) ? "Dark" : settings.Theme;
        mainVm.OverlayOpacity = settings.Opacity <= 0 ? 0.88 : settings.Opacity;
        mainVm.OverlayShowTranslation = settings.ShowTranslation;
    }

    private static void ApplyOverlaySettingsToOverlay(
        MainWindowViewModel mainVm,
        OverlayWindowViewModel overlayVm,
        OverlayWindow overlayWindow,
        OverlaySettings settings)
    {
        overlayVm.OverlayFontSize = mainVm.OverlayFontSize;
        overlayVm.OverlayLineHeight = mainVm.OverlayLineHeight;
        overlayVm.OverlayWidth = mainVm.OverlayWidth;
        overlayVm.OverlayHeight = mainVm.OverlayHeight;
        overlayVm.OverlayTheme = mainVm.OverlayTheme;
        overlayVm.OverlayOpacity = mainVm.OverlayOpacity;
        overlayVm.OverlayShowTranslation = mainVm.OverlayShowTranslation;

        if (settings.PositionX.HasValue && settings.PositionY.HasValue)
        {
            overlayWindow.Position = new PixelPoint(settings.PositionX.Value, settings.PositionY.Value);
        }
    }

    private void SaveOverlaySettings(MainWindowViewModel mainVm)
    {
        if (_settingsStore is null)
        {
            return;
        }

        _settings = _settingsStore.Load();
        _settings.Overlay.FontSize = mainVm.OverlayFontSize;
        _settings.Overlay.LineHeight = mainVm.OverlayLineHeight;
        _settings.Overlay.Theme = mainVm.OverlayTheme;
        _settings.Overlay.Opacity = mainVm.OverlayOpacity;
        _settings.Overlay.ShowTranslation = mainVm.OverlayShowTranslation;
        _settingsStore.Save(_settings);
    }

    private void SaveOverlayPosition(OverlayWindow overlayWindow, MainWindowViewModel mainVm)
    {
        if (_settingsStore is null)
        {
            return;
        }

        _settings = _settingsStore.Load();
        _settings.Overlay.PositionX = overlayWindow.Position.X;
        _settings.Overlay.PositionY = overlayWindow.Position.Y;
        _settings.Overlay.Width = overlayWindow.Width;
        _settings.Overlay.Height = overlayWindow.Height;
        mainVm.OverlayWidth = overlayWindow.Width;
        mainVm.OverlayHeight = overlayWindow.Height;
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
