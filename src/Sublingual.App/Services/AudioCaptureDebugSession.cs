using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;
using Sublingual.App.Services.Translation;
using Sublingual.Infrastructure.Audio.Processing;

namespace Sublingual.App.Services;

public class AudioCaptureDebugSession : IDisposable
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly Sublingual.Application.Audio.StartCaptureUseCase _startCaptureUseCase;
    private readonly Sublingual.Application.Audio.StopCaptureUseCase _stopCaptureUseCase;
    private readonly Sublingual.Application.Audio.ProcessAudioChunkUseCase _processAudioChunkUseCase;
    private readonly Sublingual.Application.Audio.TranscribeAudioChunkUseCase _transcribeAudioChunkUseCase;
    private readonly ITranslationExecutionService _translationService;
    private readonly CaptureSessionStorage _captureSessionStorage;
    private readonly AudioFormatNormalizer _audioFormatNormalizer;
    private readonly VoskInputVerifier _voskInputVerifier;
    private readonly AppSettingsStore _settingsStore;
    private readonly VoskTranscriptionService? _voskTranscriptionService;
    private readonly SemaphoreSlim _pipelineGate = new(1, 1);
    private WaveFileCaptureVerifier? _captureVerifier;
    private string? _currentOutputPath;
    private string _currentDeviceName = "Unknown";
    private string _currentSourceLanguage = "en";
    private string _currentTargetLanguage = "vi";
    private bool _translatePartials;
    private double _capturedDurationSeconds;
    private DateTimeOffset _currentSessionCreatedAt;
    private bool _disposed;

    public AudioCaptureDebugSession(
        IAudioCaptureService audioCaptureService,
        Sublingual.Application.Audio.StartCaptureUseCase startCaptureUseCase,
        Sublingual.Application.Audio.StopCaptureUseCase stopCaptureUseCase,
        Sublingual.Application.Audio.ProcessAudioChunkUseCase processAudioChunkUseCase,
        Sublingual.Application.Audio.TranscribeAudioChunkUseCase transcribeAudioChunkUseCase,
        ITranslationExecutionService translationService,
        CaptureSessionStorage captureSessionStorage,
        AudioFormatNormalizer audioFormatNormalizer,
        VoskInputVerifier voskInputVerifier,
        AppSettingsStore settingsStore,
        VoskTranscriptionService? voskTranscriptionService = null)
    {
        _audioCaptureService = audioCaptureService;
        _startCaptureUseCase = startCaptureUseCase;
        _stopCaptureUseCase = stopCaptureUseCase;
        _processAudioChunkUseCase = processAudioChunkUseCase;
        _transcribeAudioChunkUseCase = transcribeAudioChunkUseCase;
        _translationService = translationService;
        _captureSessionStorage = captureSessionStorage;
        _audioFormatNormalizer = audioFormatNormalizer;
        _voskInputVerifier = voskInputVerifier;
        _settingsStore = settingsStore;
        _voskTranscriptionService = voskTranscriptionService;

        _audioCaptureService.AudioChunkCaptured += OnAudioChunkCaptured;
    }

    public AudioCaptureState State => _audioCaptureService.State;

    public event EventHandler<AudioChunk>? ChunkObserved;

    public event EventHandler<TranscriptPreviewUpdate>? TranscriptPreviewUpdated;

    public Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        return _audioCaptureService.GetAvailableDevicesAsync(AudioSourceType.System, cancellationToken);
    }

    public async Task StartAsync(string? deviceId, string deviceName, string outputPath, CancellationToken cancellationToken = default)
    {
        _voskTranscriptionService?.ResetSession();
        _translationService.ClearCache();
        DisposeVerifier();
        _captureVerifier = new WaveFileCaptureVerifier(outputPath);
        _currentOutputPath = outputPath;
        _currentDeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Unknown" : deviceName;
        var translationSettings = _settingsStore.Load().Translation;
        _currentSourceLanguage = NormalizeLanguage(translationSettings.SourceLanguage, "en");
        _currentTargetLanguage = NormalizeLanguage(translationSettings.TargetLanguage, "vi");
        _translatePartials = translationSettings.TranslatePartials;
        _capturedDurationSeconds = 0;
        _currentSessionCreatedAt = DateTimeOffset.UtcNow;
        _captureSessionStorage.SaveSessionMetadata(
            outputPath,
            new Models.CaptureSessionMetadata
            {
                ModelName = _voskTranscriptionService?.CurrentModelName ?? "Unknown",
                DeviceName = _currentDeviceName,
                Language = _currentSourceLanguage,
                DurationSeconds = 0,
                CreatedAt = _currentSessionCreatedAt,
            });

        await _startCaptureUseCase.ExecuteAsync(
            new AudioCaptureRequest(AudioSourceType.System, deviceId, 16_000, 1),
            cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _stopCaptureUseCase.ExecuteAsync(cancellationToken);
        _voskTranscriptionService?.ResetSession();
        _translationService.ClearCache();

        if (!string.IsNullOrWhiteSpace(_currentOutputPath))
        {
            _captureSessionStorage.SaveSessionMetadata(
                _currentOutputPath,
                new Models.CaptureSessionMetadata
                {
                    ModelName = _voskTranscriptionService?.CurrentModelName ?? "Unknown",
                    DeviceName = _currentDeviceName,
                    Language = _currentSourceLanguage,
                    DurationSeconds = _capturedDurationSeconds,
                    CreatedAt = _currentSessionCreatedAt,
                });
        }

        DisposeVerifier();
        _currentOutputPath = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _audioCaptureService.AudioChunkCaptured -= OnAudioChunkCaptured;
        DisposeVerifier();

        if (_audioCaptureService is IDisposable disposableCaptureService)
        {
            disposableCaptureService.Dispose();
        }

        _pipelineGate.Dispose();
        _disposed = true;
    }

    private void OnAudioChunkCaptured(object? sender, AudioChunk inputChunk)
    {
        _ = ProcessChunkAsync(inputChunk);
    }

    private async Task ProcessChunkAsync(AudioChunk inputChunk)
    {
        await _pipelineGate.WaitAsync();
        try
        {
            var processedChunks = _processAudioChunkUseCase.Execute(inputChunk);
            foreach (var chunk in processedChunks)
            {
                _captureVerifier?.Append(chunk);
                _capturedDurationSeconds += chunk.Duration.TotalSeconds;
                ChunkObserved?.Invoke(this, chunk);
                await PublishTranscriptPreviewAsync(chunk);
            }
        }
        catch (Exception ex)
        {
            TranscriptPreviewUpdated?.Invoke(
                this,
                new TranscriptPreviewUpdate(
                    string.Empty,
                    string.Empty,
                    $"Capture pipeline error: {ex.Message}",
                    string.Empty,
                    DateTimeOffset.Now,
                    "Error",
                    ex.Message,
                    false));
        }
        finally
        {
            _pipelineGate.Release();
        }
    }

    private async Task PublishTranscriptPreviewAsync(AudioChunk chunk)
    {
        var normalizedChunk = _audioFormatNormalizer.NormalizeForSpeechRecognition(chunk);
        _voskInputVerifier.Verify(normalizedChunk);

        var transcription = await _transcribeAudioChunkUseCase.ExecuteAsync(
            new TranscriptionRequest(normalizedChunk, _currentSourceLanguage));

        var partialText = transcription.Segments.LastOrDefault(segment => segment.IsPartial)?.Text ?? string.Empty;
        var finalText = transcription.Segments.LastOrDefault(segment => !segment.IsPartial)?.Text ?? string.Empty;
        var translationTarget = !string.IsNullOrWhiteSpace(finalText)
            ? finalText
            : _translatePartials
                ? partialText
                : string.Empty;

        var shouldTranslate = !string.IsNullOrWhiteSpace(translationTarget)
            && !string.Equals(_currentSourceLanguage, _currentTargetLanguage, StringComparison.OrdinalIgnoreCase);

        var translation = !shouldTranslate
            ? new TranslationExecutionResult(
                new TranslationResult(translationTarget, string.Empty, _currentTargetLanguage),
                string.Equals(_currentSourceLanguage, _currentTargetLanguage, StringComparison.OrdinalIgnoreCase)
                    ? "SkippedSameLanguage"
                    : "SkippedNoTranslationTarget",
                string.Equals(_currentSourceLanguage, _currentTargetLanguage, StringComparison.OrdinalIgnoreCase)
                    ? ["Skipped: source and target languages match"]
                    : ["Skipped: partial translation disabled or no translatable text"],
                false
            )
            : await _translationService.TranslateWithDiagnosticsAsync(
                new TranslationRequest(translationTarget, _currentSourceLanguage, _currentTargetLanguage));

        var partialTranslatedText = string.IsNullOrWhiteSpace(finalText) ? translation.Result.TranslatedText : string.Empty;
        var finalTranslatedText = string.IsNullOrWhiteSpace(finalText) ? string.Empty : translation.Result.TranslatedText;

        var update = new TranscriptPreviewUpdate(
            partialText,
            partialTranslatedText,
            finalText,
            finalTranslatedText,
            DateTimeOffset.Now,
            translation.ProviderName,
            $"{_voskInputVerifier.Describe(normalizedChunk)} | {string.Join(" | ", translation.AttemptLog)}",
            translation.IsCacheHit);

        if (!string.IsNullOrWhiteSpace(_currentOutputPath))
        {
            _captureSessionStorage.SaveTranscriptEntry(
                _currentOutputPath,
                new Models.SavedTranscriptEntry
                {
                    PartialText = update.PartialText,
                    PartialTranslatedText = update.PartialTranslatedText,
                    FinalText = update.FinalText,
                    FinalTranslatedText = update.FinalTranslatedText,
                    UpdatedAt = update.UpdatedAt,
                });
        }

        TranscriptPreviewUpdated?.Invoke(this, update);
    }

    private void DisposeVerifier()
    {
        _captureVerifier?.Dispose();
        _captureVerifier = null;
    }

    private static string NormalizeLanguage(string? language, string fallback)
    {
        return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase)
            ? "zh"
            : string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase)
                ? "vi"
                : string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                    ? "en"
                    : fallback;
    }
}
