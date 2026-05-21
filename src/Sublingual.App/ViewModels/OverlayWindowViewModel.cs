using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Sublingual.App.Services;
using Sublingual.App.Services.Translation;

namespace Sublingual.App.ViewModels;

public sealed partial class OverlayWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private bool _disposed;

    [ObservableProperty] private string partialOriginalText = string.Empty;
    [ObservableProperty] private string finalTranslatedText = string.Empty;
    [ObservableProperty] private string statusText = "Overlay ready.";
    [ObservableProperty] private double overlayFontSize = 26;
    [ObservableProperty] private double overlayLineHeight = 1.35;
    [ObservableProperty] private double overlayWidth = 720;
    [ObservableProperty] private double overlayHeight = 200;
    [ObservableProperty] private string overlayTheme = "Dark";
    [ObservableProperty] private double overlayOpacity = 0.88;
    [ObservableProperty] private bool overlayShowTranslation = true;
    [ObservableProperty] private bool isFixedToBottom = true;
    [ObservableProperty] private int scrollRequestVersion;

    public ObservableCollection<OverlayTranscriptLineViewModel> TranscriptLines { get; } = [];

    public double OverlayTranslationFontSize => Math.Max(14, OverlayFontSize - 4);
    public double EffectiveOverlayLineHeight => OverlayShowTranslation ? OverlayLineHeight : Math.Max(0.90, OverlayLineHeight - 0.18);
    public double OverlayPrimaryLineHeight => OverlayFontSize * EffectiveOverlayLineHeight;
    public double OverlaySecondaryLineHeight => OverlayTranslationFontSize * EffectiveOverlayLineHeight;
    public bool HasPartial => !string.IsNullOrWhiteSpace(PartialOriginalText);
    public bool HasFinalTranslation => OverlayShowTranslation && !string.IsNullOrWhiteSpace(FinalTranslatedText);
    public bool IsDarkTheme => string.Equals(OverlayTheme, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsLightTheme => string.Equals(OverlayTheme, "Light", StringComparison.OrdinalIgnoreCase);
    public bool HasCaption => TranscriptLines.Count > 0 || HasPartial;
    public bool ShowPlaceholder => TranscriptLines.Count == 0 && !HasPartial;
    public string OverlayFollowButtonTooltip => IsFixedToBottom ? "Following newest lines" : "Jump to bottom and follow";
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
        OnPropertyChanged(nameof(OverlayPrimaryLineHeight));
        OnPropertyChanged(nameof(OverlaySecondaryLineHeight));
    }

    partial void OnOverlayLineHeightChanged(double value)
    {
        OnPropertyChanged(nameof(EffectiveOverlayLineHeight));
        OnPropertyChanged(nameof(OverlayPrimaryLineHeight));
        OnPropertyChanged(nameof(OverlaySecondaryLineHeight));
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

    partial void OnIsFixedToBottomChanged(bool value)
    {
        OnPropertyChanged(nameof(OverlayFollowButtonTooltip));
    }

    partial void OnPartialOriginalTextChanged(string value)
    {
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(HasCaption));
        OnPropertyChanged(nameof(HasPartial));
    }

    partial void OnFinalTranslatedTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasFinalTranslation));
    }

    partial void OnOverlayShowTranslationChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveOverlayLineHeight));
        OnPropertyChanged(nameof(OverlayPrimaryLineHeight));
        OnPropertyChanged(nameof(OverlaySecondaryLineHeight));
        OnPropertyChanged(nameof(HasFinalTranslation));
    }

    private void OnTranscriptPreviewUpdated(object? sender, TranscriptPreviewUpdate update)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            var partialText = update.PartialText ?? string.Empty;
            PartialOriginalText = partialText;
            if (!string.IsNullOrWhiteSpace(update.FinalText))
            {
                TranscriptLines.Add(new OverlayTranscriptLineViewModel(
                    update.FinalText,
                    update.FinalTranslatedText,
                    update.UpdatedAt));

                while (TranscriptLines.Count > 80)
                {
                    TranscriptLines.RemoveAt(0);
                }

                PartialOriginalText = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(update.FinalTranslatedText))
            {
                FinalTranslatedText = update.FinalTranslatedText;
            }

            StatusText = $"Updated {update.UpdatedAt:HH:mm:ss}";
            if (IsFixedToBottom)
            {
                ScrollRequestVersion += 1;
            }

            OnPropertyChanged(nameof(HasCaption));
            OnPropertyChanged(nameof(ShowPlaceholder));
        });
    }

    public void FollowToBottom()
    {
        IsFixedToBottom = true;
        ScrollRequestVersion += 1;
    }

    private static AudioCaptureDebugSession CreateDesignTimeSession()
    {
        var captureService = DesignTimeAudioCaptureService.Instance;
        var settingsStore = new AppSettingsStore();
        return new AudioCaptureDebugSession(
            captureService,
            new Sublingual.Application.Audio.StartCaptureUseCase(captureService),
            new Sublingual.Application.Audio.StopCaptureUseCase(captureService),
            new Sublingual.Application.Audio.ProcessAudioChunkUseCase(
                new Sublingual.Infrastructure.Audio.Processing.PassthroughAudioChunkProcessor()),
            new Sublingual.Application.Audio.TranscribeAudioChunkUseCase(new MockTranscriptionService()),
            new ConfigurableTranslationService(
                [
                    new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                    new LibreTranslateTranslationProvider(new HttpClient()),
                ],
                settingsStore
            ),
            new CaptureSessionStorage(settingsStore),
            new Sublingual.Infrastructure.Audio.Processing.AudioFormatNormalizer(),
            new Sublingual.Infrastructure.Audio.Processing.VoskInputVerifier(),
            settingsStore,
            new RealtimeTranslationScheduler(
                new ConfigurableTranslationService(
                    [
                        new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                        new LibreTranslateTranslationProvider(new HttpClient()),
                    ],
                    settingsStore
                )));
    }

    private static string ToHexColor(double opacity, byte red, byte green, byte blue)
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        return $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
    }
}

public sealed class OverlayTranscriptLineViewModel(string originalText, string translatedText, DateTimeOffset updatedAt)
{
    public string OriginalText { get; } = originalText;
    public string TranslatedText { get; } = translatedText ?? string.Empty;
    public DateTimeOffset UpdatedAt { get; } = updatedAt;
    public bool HasTranslation => !string.IsNullOrWhiteSpace(TranslatedText);
}
