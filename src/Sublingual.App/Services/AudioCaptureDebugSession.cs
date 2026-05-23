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
    private readonly RealtimeTranslationScheduler _translationScheduler;
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
    private long _sessionGeneration;
    private long _realtimeTranscriptSequenceId;
    private int _stableSegmentIndex;
    private string _currentDraftSegmentId = CreateDraftSegmentId();
    private string _currentDraftSourceText = string.Empty;
    private string _translationSessionId = Guid.NewGuid().ToString("N");
    private readonly Lock _translationStateLock = new();
    private readonly Dictionary<string, string> _pendingStableTranslations = new(StringComparer.Ordinal);
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
        RealtimeTranslationScheduler translationScheduler,
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
        _translationScheduler = translationScheduler;
        _voskTranscriptionService = voskTranscriptionService;

        _audioCaptureService.AudioChunkCaptured += OnAudioChunkCaptured;
        _translationScheduler.TranslationCompleted += OnTranslationCompleted;
    }

    public AudioCaptureState State => _audioCaptureService.State;

    public event EventHandler<AudioChunk>? ChunkObserved;

    public event EventHandler<TranscriptPreviewUpdate>? TranscriptPreviewUpdated;

    public event EventHandler<RealtimeTranscriptEvent>? RealtimeTranscriptEventPublished;

    public Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        return _audioCaptureService.GetAvailableDevicesAsync(AudioSourceType.System, cancellationToken);
    }

    public async Task StartAsync(string? deviceId, string deviceName, string outputPath, string sessionTitle = "", string sessionTreePath = "", CancellationToken cancellationToken = default)
    {
        _voskTranscriptionService?.ResetSession();
        _translationService.ClearCache();
        PublishRealtimeTranscriptEvent(new TranscriptOverlayReset(
            NextRealtimeTranscriptSequenceId(),
            DateTimeOffset.Now));
        DisposeVerifier();
        _captureVerifier = new WaveFileCaptureVerifier(outputPath);
        _currentOutputPath = outputPath;
        _currentDeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Unknown" : deviceName;
        var translationSettings = _settingsStore.Load().Translation;
        _currentSourceLanguage = NormalizeLanguage(translationSettings.SourceLanguage, "en");
        _currentTargetLanguage = NormalizeLanguage(translationSettings.TargetLanguage, "vi");
        _translatePartials = translationSettings.TranslatePartials;
        await ResetRealtimeProviderSessionIfNeededAsync(translationSettings, _translationSessionId, cancellationToken);
        _capturedDurationSeconds = 0;
        _currentSessionCreatedAt = DateTimeOffset.UtcNow;
        _sessionGeneration += 1;
        _realtimeTranscriptSequenceId = 0;
        _stableSegmentIndex = 0;
        _currentDraftSegmentId = CreateDraftSegmentId();
        _currentDraftSourceText = string.Empty;
        _translationSessionId = Guid.NewGuid().ToString("N");
        lock (_translationStateLock)
        {
            _pendingStableTranslations.Clear();
        }
        _captureSessionStorage.SaveSessionMetadata(
            outputPath,
            new Models.CaptureSessionMetadata
            {
                Title = sessionTitle,
                TreePath = sessionTreePath,
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
        var translationSettings = _settingsStore.Load().Translation;
        var sessionIdToReset = _translationSessionId;
        await _stopCaptureUseCase.ExecuteAsync(cancellationToken);
        _voskTranscriptionService?.ResetSession();
        _translationService.ClearCache();
        _sessionGeneration += 1;
        lock (_translationStateLock)
        {
            _pendingStableTranslations.Clear();
            _currentDraftSegmentId = CreateDraftSegmentId();
            _currentDraftSourceText = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_currentOutputPath))
        {
            _captureSessionStorage.SaveSessionMetadata(
                _currentOutputPath,
                new Models.CaptureSessionMetadata
                {
                    Title = _captureSessionStorage.GetSessionMetadata(Path.Combine(Path.GetDirectoryName(_currentOutputPath) ?? string.Empty, "session.json"))?.Title ?? string.Empty,
                    TreePath = _captureSessionStorage.GetSessionMetadata(Path.Combine(Path.GetDirectoryName(_currentOutputPath) ?? string.Empty, "session.json"))?.TreePath ?? string.Empty,
                    ModelName = _voskTranscriptionService?.CurrentModelName ?? "Unknown",
                    DeviceName = _currentDeviceName,
                    Language = _currentSourceLanguage,
                    DurationSeconds = _capturedDurationSeconds,
                    CreatedAt = _currentSessionCreatedAt,
                });
        }

        DisposeVerifier();
        _currentOutputPath = null;
        await ResetRealtimeProviderSessionIfNeededAsync(translationSettings, sessionIdToReset, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _audioCaptureService.AudioChunkCaptured -= OnAudioChunkCaptured;
        _translationScheduler.TranslationCompleted -= OnTranslationCompleted;
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
                await ProcessTranscriptAsync(chunk);
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

    private async Task ProcessTranscriptAsync(AudioChunk chunk)
    {
        var normalizedChunk = _audioFormatNormalizer.NormalizeForSpeechRecognition(chunk);
        _voskInputVerifier.Verify(normalizedChunk);

        var transcription = await _transcribeAudioChunkUseCase.ExecuteAsync(
            new TranscriptionRequest(normalizedChunk, _currentSourceLanguage));

        var partialText = transcription.Segments.LastOrDefault(segment => segment.IsPartial)?.Text ?? string.Empty;
        var finalText = transcription.Segments.LastOrDefault(segment => !segment.IsPartial)?.Text ?? string.Empty;
        var updatedAt = DateTimeOffset.Now;
        var diagnostics = _voskInputVerifier.Describe(normalizedChunk);
        PublishTranscriptEvents(partialText, finalText, updatedAt);

        ScheduleTranslation(partialText, finalText, updatedAt);
        if (string.IsNullOrWhiteSpace(finalText) && !_translatePartials)
        {
            PublishRealtimeTranscriptEvent(new TranscriptTranslationChanged(
                NextRealtimeTranscriptSequenceId(),
                _currentDraftSegmentId,
                TranscriptTranslationTarget.Draft,
                partialText,
                string.Empty,
                false,
                "SkippedNoTranslationTarget",
                false,
                updatedAt));
            EmitTranscriptPreviewUpdate(partialText, string.Empty, finalText, string.Empty, "SkippedNoTranslationTarget", diagnostics, false, updatedAt);
        }
    }

    private void PublishTranscriptEvents(string partialText, string finalText, DateTimeOffset updatedAt)
    {
        if (!string.IsNullOrWhiteSpace(partialText))
        {
            _currentDraftSourceText = partialText;
            PersistTranscriptSegment(_currentDraftSegmentId, partialText, string.Empty, false, updatedAt);
            PublishRealtimeTranscriptEvent(new DraftTranscriptChanged(
                NextRealtimeTranscriptSequenceId(),
                _currentDraftSegmentId,
                partialText,
                updatedAt));
        }

        if (!string.IsNullOrWhiteSpace(finalText))
        {
            if (!string.IsNullOrWhiteSpace(_currentOutputPath))
            {
                _captureSessionStorage.DeleteTranscriptEntry(_currentOutputPath, _currentDraftSegmentId);
            }

            _stableSegmentIndex += 1;
            var stableSegmentId = BuildStableSegmentId(_stableSegmentIndex);
            PersistTranscriptSegment(stableSegmentId, finalText, string.Empty, true, updatedAt);
            PublishRealtimeTranscriptEvent(new StableTranscriptCommitted(
                NextRealtimeTranscriptSequenceId(),
                stableSegmentId,
                finalText,
                updatedAt));
            _currentDraftSegmentId = CreateDraftSegmentId();
            _currentDraftSourceText = string.Empty;
        }
    }

    private void ScheduleTranslation(string partialText, string finalText, DateTimeOffset updatedAt)
    {
        if (!string.IsNullOrWhiteSpace(finalText))
        {
            var segmentId = BuildStableSegmentId(_stableSegmentIndex);
            PublishRealtimeTranscriptEvent(new TranscriptTranslationChanged(
                NextRealtimeTranscriptSequenceId(),
                segmentId,
                TranscriptTranslationTarget.StableSegment,
                finalText,
                string.Empty,
                true,
                "Pending",
                false,
                updatedAt));

            _translationScheduler.EnqueueStable(new StableTranslationRequest(
                _sessionGeneration,
                _translationSessionId,
                segmentId,
                NextRealtimeTranscriptSequenceId(),
                finalText,
                _currentSourceLanguage,
                _currentTargetLanguage,
                true));
            lock (_translationStateLock)
            {
                _pendingStableTranslations[segmentId] = finalText;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(partialText) || !_translatePartials)
        {
            return;
        }

        PublishRealtimeTranscriptEvent(new TranscriptTranslationChanged(
            NextRealtimeTranscriptSequenceId(),
            _currentDraftSegmentId,
            TranscriptTranslationTarget.Draft,
            partialText,
            string.Empty,
            true,
            "Pending",
            false,
            updatedAt));

        _translationScheduler.EnqueueDraft(new DraftTranslationRequest(
            _sessionGeneration,
            _translationSessionId,
            _currentDraftSegmentId,
            NextRealtimeTranscriptSequenceId(),
            partialText,
            _currentSourceLanguage,
            _currentTargetLanguage,
            false));
    }

    private void OnTranslationCompleted(object? sender, RealtimeTranslationCompleted completed)
    {
        if (completed.SessionGeneration != _sessionGeneration)
        {
            return;
        }

        if (completed.Target == TranscriptTranslationTarget.Draft)
        {
            if (!string.Equals(completed.SegmentId, _currentDraftSegmentId, StringComparison.Ordinal)
                || !string.Equals(completed.SourceText, GetCurrentDraftSourceText(), StringComparison.Ordinal))
            {
                return;
            }
        }
        else
        {
            lock (_translationStateLock)
            {
                if (!_pendingStableTranslations.TryGetValue(completed.SegmentId, out var expectedSourceText)
                    || !string.Equals(expectedSourceText, completed.SourceText, StringComparison.Ordinal))
                {
                    return;
                }

                _pendingStableTranslations.Remove(completed.SegmentId);
            }
        }

        var diagnostics = completed.AttemptLog.Count == 0
            ? completed.ProviderName
            : string.Join(" | ", completed.AttemptLog);

        PublishRealtimeTranscriptEvent(new TranscriptTranslationChanged(
            NextRealtimeTranscriptSequenceId(),
            completed.SegmentId,
            completed.Target,
            completed.SourceText,
            completed.TranslatedText,
            false,
            completed.ProviderName,
            completed.IsCacheHit,
            completed.UpdatedAt));

        if (completed.Target == TranscriptTranslationTarget.Draft)
        {
            PersistTranscriptSegment(completed.SegmentId, completed.SourceText, completed.TranslatedText, false, completed.UpdatedAt);
            EmitTranscriptPreviewUpdate(
                completed.SourceText,
                completed.TranslatedText,
                string.Empty,
                string.Empty,
                completed.ProviderName,
                diagnostics,
                completed.IsCacheHit,
                completed.UpdatedAt);
            return;
        }

        PersistTranscriptSegment(completed.SegmentId, completed.SourceText, completed.TranslatedText, true, completed.UpdatedAt);
        EmitTranscriptPreviewUpdate(
            string.Empty,
            string.Empty,
            completed.SourceText,
            completed.TranslatedText,
            completed.ProviderName,
            diagnostics,
            completed.IsCacheHit,
            completed.UpdatedAt);
    }

    private void EmitTranscriptPreviewUpdate(
        string partialText,
        string partialTranslatedText,
        string finalText,
        string finalTranslatedText,
        string providerName,
        string diagnostics,
        bool isCacheHit,
        DateTimeOffset updatedAt)
    {
        var update = new TranscriptPreviewUpdate(
            partialText,
            partialTranslatedText,
            finalText,
            finalTranslatedText,
            updatedAt,
            providerName,
            diagnostics,
            isCacheHit);

        TranscriptPreviewUpdated?.Invoke(this, update);
    }

    private void PersistTranscriptSegment(
        string segmentId,
        string originalText,
        string translatedText,
        bool isFinal,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(_currentOutputPath) || string.IsNullOrWhiteSpace(segmentId) || string.IsNullOrWhiteSpace(originalText))
        {
            return;
        }

        _captureSessionStorage.SaveTranscriptEntry(
            _currentOutputPath,
            new Models.SavedTranscriptEntry
            {
                SegmentId = segmentId,
                OriginalText = originalText,
                TranslatedText = translatedText ?? string.Empty,
                IsFinal = isFinal,
                UpdatedAt = updatedAt,
            });
    }

    private string GetCurrentDraftSourceText()
    {
        return _currentDraftSourceText;
    }

    private void DisposeVerifier()
    {
        _captureVerifier?.Dispose();
        _captureVerifier = null;
    }

    private void PublishRealtimeTranscriptEvent(RealtimeTranscriptEvent transcriptEvent)
    {
        RealtimeTranscriptEventPublished?.Invoke(this, transcriptEvent);
    }

    private long NextRealtimeTranscriptSequenceId()
    {
        return Interlocked.Increment(ref _realtimeTranscriptSequenceId);
    }

    private static string BuildStableSegmentId(int stableSegmentIndex)
    {
        return $"stable-{stableSegmentIndex:D8}";
    }

    private static string CreateDraftSegmentId()
    {
        return $"draft-{Guid.NewGuid():N}";
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

    private async Task ResetRealtimeProviderSessionIfNeededAsync(
        Models.TranslationSettings settings,
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (_translationService is not ConfigurableTranslationService configurableTranslationService)
        {
            return;
        }

        await configurableTranslationService.ResetRealtimeProviderSessionAsync(settings, sessionId, cancellationToken);
    }
}
