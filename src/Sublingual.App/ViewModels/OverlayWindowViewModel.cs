using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Sublingual.App.Services;

namespace Sublingual.App.ViewModels;

public sealed partial class OverlayWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private bool _disposed;

    [ObservableProperty] private string partialOriginalText = string.Empty;
    [ObservableProperty] private string finalOriginalText = string.Empty;
    [ObservableProperty] private string partialTranslatedText = string.Empty;
    [ObservableProperty] private string finalTranslatedText = string.Empty;
    [ObservableProperty] private string statusText = "Overlay ready.";
    [ObservableProperty] private double overlayFontSize = 26;
    [ObservableProperty] private double overlayWidth = 720;
    [ObservableProperty] private double overlayHeight = 200;
    [ObservableProperty] private string overlayTheme = "Dark";
    [ObservableProperty] private double overlayOpacity = 0.88;

    public double OverlayTranslationFontSize => Math.Max(14, OverlayFontSize - 4);
    public bool HasPartial => !string.IsNullOrWhiteSpace(PartialOriginalText);
    public bool HasFinal => !string.IsNullOrWhiteSpace(FinalOriginalText);
    public bool HasFinalTranslation => !string.IsNullOrWhiteSpace(FinalTranslatedText);
    public bool IsDarkTheme => string.Equals(OverlayTheme, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsLightTheme => string.Equals(OverlayTheme, "Light", StringComparison.OrdinalIgnoreCase);
    public bool HasCaption => HasFinal || HasPartial;
    public bool ShowPlaceholder => !HasFinal && !HasPartial;
    public string DisplayCaptionText => HasFinal ? FinalOriginalText : PartialOriginalText;
    public double OverlayShadowOpacity => Math.Clamp(0.18 + (OverlayOpacity * 0.32), 0.18, 0.5);

    public string DarkOverlayBackground => ToHexColor(OverlayOpacity, 14, 19, 28);
    public string DarkOverlayBorder => ToHexColor(Math.Clamp(OverlayOpacity + 0.08, 0.35, 1.0), 56, 70, 92);
    public string DarkOverlayCloseBackground => ToHexColor(Math.Clamp(OverlayOpacity + 0.02, 0.35, 1.0), 28, 40, 55);

    public string LightOverlayBackground => ToHexColor(Math.Clamp(0.80 + (OverlayOpacity * 0.18), 0.80, 0.98), 245, 247, 250);
    public string LightOverlayBorder => ToHexColor(Math.Clamp(0.70 + (OverlayOpacity * 0.20), 0.70, 1.0), 210, 218, 229);
    public string LightOverlayCloseBackground => ToHexColor(Math.Clamp(0.72 + (OverlayOpacity * 0.16), 0.72, 1.0), 229, 234, 241);

    public OverlayWindowViewModel(AudioCaptureDebugSession session)
    {
        _session = session;
        _session.TranscriptPreviewUpdated += OnTranscriptPreviewUpdated;
    }

    // Design-time constructor
    public OverlayWindowViewModel()
        : this(CreateDesignTimeSession())
    {
    }

    public void Dispose()
    {
        if (_disposed) return;
        _session.TranscriptPreviewUpdated -= OnTranscriptPreviewUpdated;
        _disposed = true;
    }

    partial void OnOverlayFontSizeChanged(double value)
    {
        OnPropertyChanged(nameof(OverlayTranslationFontSize));
    }

    partial void OnOverlayThemeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
    }

    partial void OnOverlayOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(OverlayShadowOpacity));
        OnPropertyChanged(nameof(DarkOverlayBackground));
        OnPropertyChanged(nameof(DarkOverlayBorder));
        OnPropertyChanged(nameof(DarkOverlayCloseBackground));
        OnPropertyChanged(nameof(LightOverlayBackground));
        OnPropertyChanged(nameof(LightOverlayBorder));
        OnPropertyChanged(nameof(LightOverlayCloseBackground));
    }

    partial void OnFinalOriginalTextChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayCaptionText));
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(HasCaption));
        OnPropertyChanged(nameof(HasFinal));
    }

    partial void OnPartialOriginalTextChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayCaptionText));
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(HasCaption));
        OnPropertyChanged(nameof(HasPartial));
    }

    partial void OnFinalTranslatedTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasFinalTranslation));
    }

    private void OnTranscriptPreviewUpdated(object? sender, TranscriptPreviewUpdate update)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            PartialOriginalText = update.PartialText ?? string.Empty;
            PartialTranslatedText = update.PartialTranslatedText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(update.FinalText))
                FinalOriginalText = update.FinalText;
            if (!string.IsNullOrWhiteSpace(update.FinalTranslatedText))
                FinalTranslatedText = update.FinalTranslatedText;
            StatusText = $"Updated {update.UpdatedAt:HH:mm:ss}";
        });
    }

    private static AudioCaptureDebugSession CreateDesignTimeSession()
    {
        var captureService = DesignTimeAudioCaptureService.Instance;
        return new AudioCaptureDebugSession(
            captureService,
            new Sublingual.Application.Audio.StartCaptureUseCase(captureService),
            new Sublingual.Application.Audio.StopCaptureUseCase(captureService),
            new Sublingual.Application.Audio.ProcessAudioChunkUseCase(
                new Sublingual.Infrastructure.Audio.Processing.PassthroughAudioChunkProcessor()),
            new Sublingual.Application.Audio.TranscribeAudioChunkUseCase(new MockTranscriptionService()),
            new Sublingual.Application.Audio.TranslateTranscriptUseCase(new MockTranslationService()),
            new CaptureSessionStorage());
    }

    private static string ToHexColor(double opacity, byte red, byte green, byte blue)
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        return $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
    }
}
