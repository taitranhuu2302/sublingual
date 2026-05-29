using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Sublingual.App.Services;
using Sublingual.App.Services.Translation;

namespace Sublingual.App.ViewModels;

/// <summary>
/// Drives the overlay window.
///
/// Model is intentionally simple:
///   - <see cref="Lines"/> = list of committed (stable) caption pairs + one optional live draft at tail
///   - The draft slot is always the last item and is replaced/mutated in-place
///   - We never map UI lines to backend SegmentIds after commit — avoids stale-event trash
/// </summary>
public sealed partial class OverlayWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private bool _disposed;

    // Tracks which backend segmentId owns the *current* draft translation slot.
    // Streaming chunks are only accepted if they match this id.
    private string? _draftTranslationSegmentId;

    // ── Display settings ──────────────────────────────────────────────────────
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

    // ── Caption lines ─────────────────────────────────────────────────────────
    /// <summary>All visible lines. Last item is the live draft (if any).</summary>
    public ObservableCollection<OverlayCaptionLine> Lines { get; } = [];

    // ── Derived display properties ────────────────────────────────────────────
    public double OverlayTranslationFontSize => Math.Max(14, OverlayFontSize - 4);
    public double EffectiveOverlayLineHeight => OverlayShowTranslation ? OverlayLineHeight : Math.Max(0.90, OverlayLineHeight - 0.18);
    public double OverlayPrimaryLineHeight => OverlayFontSize * EffectiveOverlayLineHeight;
    public double OverlaySecondaryLineHeight => OverlayTranslationFontSize * EffectiveOverlayLineHeight;
    public bool ShowPlaceholder => Lines.Count == 0;
    public bool IsDarkTheme => string.Equals(OverlayTheme, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsLightTheme => string.Equals(OverlayTheme, "Light", StringComparison.OrdinalIgnoreCase);
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
        _session.RealtimeTranscriptEventPublished += OnRealtimeTranscriptEventPublished;
    }

    public OverlayWindowViewModel() : this(CreateDesignTimeSession()) { }

    public void Dispose()
    {
        if (_disposed) return;
        _session.RealtimeTranscriptEventPublished -= OnRealtimeTranscriptEventPublished;
        _disposed = true;
    }

    public void FollowToBottom()
    {
        IsFixedToBottom = true;
        ScrollRequestVersion += 1;
    }

    // ── Property change side-effects ──────────────────────────────────────────

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

    partial void OnOverlayShowTranslationChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveOverlayLineHeight));
        OnPropertyChanged(nameof(OverlayPrimaryLineHeight));
        OnPropertyChanged(nameof(OverlaySecondaryLineHeight));
    }

    // ── Event handling ────────────────────────────────────────────────────────

    private void OnRealtimeTranscriptEventPublished(object? sender, RealtimeTranscriptEvent evt)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (evt)
            {
                case TranscriptOverlayReset reset:
                    Lines.Clear();
                    _draftTranslationSegmentId = null;
                    StatusText = $"Updated {reset.UpdatedAt:HH:mm:ss}";
                    break;

                case DraftTranscriptChanged draft:
                    // Always update/create the draft slot (last line, not committed)
                    EnsureDraftLine().OriginalText = draft.OriginalText;
                    StatusText = $"Updated {draft.UpdatedAt:HH:mm:ss}";
                    break;

                case StableTranscriptCommitted stable:
                    // Commit the draft line: freeze its text and clear translation slot
                    var draftLine = EnsureDraftLine();
                    draftLine.OriginalText = stable.OriginalText;
                    draftLine.IsCommitted = true;
                    _draftTranslationSegmentId = null;

                    // Trim to 80 lines
                    while (Lines.Count > 80)
                        Lines.RemoveAt(0);

                    StatusText = $"Updated {stable.UpdatedAt:HH:mm:ss}";
                    break;

                case TranscriptTranslationChanged { Target: TranscriptTranslationTarget.Draft } t:
                    HandleDraftTranslation(t);
                    break;

                case TranscriptTranslationChanged { Target: TranscriptTranslationTarget.StableSegment } t:
                    HandleStableTranslation(t);
                    break;
            }

            OnPropertyChanged(nameof(ShowPlaceholder));
            if (IsFixedToBottom)
                ScrollRequestVersion += 1;
        });
    }

    private void HandleDraftTranslation(TranscriptTranslationChanged t)
    {
        if (t.IsPending)
        {
            // A new draft translation is starting — register which segment owns the slot
            _draftTranslationSegmentId = t.SegmentId;
            var line = EnsureDraftLine();
            // Only show loading dots if translation area is empty
            if (string.IsNullOrEmpty(line.TranslatedText))
                line.TranslatedText = "...";
            return;
        }

        // Final or streaming chunk — only accept if segmentId matches current draft slot
        if (t.SegmentId != _draftTranslationSegmentId)
            return;

        var draft = EnsureDraftLine();
        var isStreamingChunk = string.Equals(t.ProviderName, "Streaming", StringComparison.OrdinalIgnoreCase);

        if (isStreamingChunk)
        {
            // Replace "..." placeholder then append
            if (draft.TranslatedText == "...")
                draft.TranslatedText = string.Empty;
            draft.TranslatedText += t.TranslatedText;
        }
        else
        {
            // Final result — replace
            draft.TranslatedText = t.TranslatedText ?? string.Empty;
        }

        StatusText = $"Updated {t.UpdatedAt:HH:mm:ss}";
    }

    private void HandleStableTranslation(TranscriptTranslationChanged t)
    {
        if (t.IsPending) return;

        // Find the committed line that matches this segment's *original text*
        // We match by OriginalText because SegmentId is not stored on committed lines
        var translated = t.TranslatedText ?? string.Empty;
        if (string.IsNullOrEmpty(translated)) return;

        // Walk from tail (most recent) to find the first committed line that
        // has matching SourceText and no translation yet
        for (var i = Lines.Count - 1; i >= 0; i--)
        {
            var line = Lines[i];
            if (!line.IsCommitted) continue;
            if (!string.Equals(line.OriginalText, t.SourceText, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(line.TranslatedText)) continue;

            line.TranslatedText = translated;
            break;
        }

        StatusText = $"Updated {t.UpdatedAt:HH:mm:ss}";
    }

    /// <summary>
    /// Returns the current draft line (last line that is not committed).
    /// Creates one if it does not exist.
    /// </summary>
    private OverlayCaptionLine EnsureDraftLine()
    {
        if (Lines.Count > 0 && !Lines[^1].IsCommitted)
            return Lines[^1];

        var line = new OverlayCaptionLine();
        Lines.Add(line);
        return line;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ToHexColor(double opacity, byte red, byte green, byte blue)
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        return $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";
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
                    new TranslateServiceLocalTranslationProvider(new HttpClient()),
                    new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                    new LibreTranslateTranslationProvider(new HttpClient()),
                ],
                settingsStore
            ),
            new CaptureSessionStorage(settingsStore, new SessionIndexStore(new LocalSqliteDatabase())),
            new Sublingual.Infrastructure.Audio.Processing.AudioFormatNormalizer(),
            new Sublingual.Infrastructure.Audio.Processing.VoskInputVerifier(),
            settingsStore,
            new RealtimeTranslationScheduler(
                new ConfigurableTranslationService(
                    [
                        new TranslateServiceLocalTranslationProvider(new HttpClient()),
                        new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                        new LibreTranslateTranslationProvider(new HttpClient()),
                    ],
                    settingsStore
                )),
            null,
            NullLogger<AudioCaptureDebugSession>.Instance);
    }
}
