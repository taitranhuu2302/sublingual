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

    public double OverlayTranslationFontSize => Math.Max(14, OverlayFontSize - 4);
    public bool HasPartial => !string.IsNullOrWhiteSpace(PartialOriginalText);
    public bool HasFinal => !string.IsNullOrWhiteSpace(FinalOriginalText);
    public bool HasFinalTranslation => !string.IsNullOrWhiteSpace(FinalTranslatedText);

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
            OnPropertyChanged(nameof(HasPartial));
            OnPropertyChanged(nameof(HasFinal));
            OnPropertyChanged(nameof(HasFinalTranslation));
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
            new Sublingual.Application.Audio.TranslateTranscriptUseCase(new MockTranslationService()));
    }
}

