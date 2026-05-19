using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;
using Sublingual.Infrastructure.Audio.Processing;

namespace Sublingual.App.Services;

public class AudioCaptureDebugSession : IDisposable
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly Sublingual.Application.Audio.StartCaptureUseCase _startCaptureUseCase;
    private readonly Sublingual.Application.Audio.StopCaptureUseCase _stopCaptureUseCase;
    private readonly Sublingual.Application.Audio.ProcessAudioChunkUseCase _processAudioChunkUseCase;
    private readonly Sublingual.Application.Audio.TranscribeAudioChunkUseCase _transcribeAudioChunkUseCase;
    private readonly Sublingual.Application.Audio.TranslateTranscriptUseCase _translateTranscriptUseCase;
    private readonly CaptureSessionStorage _captureSessionStorage;
    private readonly VoskTranscriptionService? _voskTranscriptionService;
    private readonly SemaphoreSlim _pipelineGate = new(1, 1);
    private WaveFileCaptureVerifier? _captureVerifier;
    private string? _currentOutputPath;
    private string _currentDeviceName = "Unknown";
    private string _currentLanguage = "en";
    private double _capturedDurationSeconds;
    private DateTimeOffset _currentSessionCreatedAt;
    private bool _disposed;

    public AudioCaptureDebugSession(
        IAudioCaptureService audioCaptureService,
        Sublingual.Application.Audio.StartCaptureUseCase startCaptureUseCase,
        Sublingual.Application.Audio.StopCaptureUseCase stopCaptureUseCase,
        Sublingual.Application.Audio.ProcessAudioChunkUseCase processAudioChunkUseCase,
        Sublingual.Application.Audio.TranscribeAudioChunkUseCase transcribeAudioChunkUseCase,
        Sublingual.Application.Audio.TranslateTranscriptUseCase translateTranscriptUseCase,
        CaptureSessionStorage captureSessionStorage,
        VoskTranscriptionService? voskTranscriptionService = null)
    {
        _audioCaptureService = audioCaptureService;
        _startCaptureUseCase = startCaptureUseCase;
        _stopCaptureUseCase = stopCaptureUseCase;
        _processAudioChunkUseCase = processAudioChunkUseCase;
        _transcribeAudioChunkUseCase = transcribeAudioChunkUseCase;
        _translateTranscriptUseCase = translateTranscriptUseCase;
        _captureSessionStorage = captureSessionStorage;
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
        DisposeVerifier();
        _captureVerifier = new WaveFileCaptureVerifier(outputPath);
        _currentOutputPath = outputPath;
        _currentDeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Unknown" : deviceName;
        _currentLanguage = "en";
        _capturedDurationSeconds = 0;
        _currentSessionCreatedAt = DateTimeOffset.UtcNow;
        _captureSessionStorage.SaveSessionMetadata(
            outputPath,
            new Models.CaptureSessionMetadata
            {
                ModelName = _voskTranscriptionService?.CurrentModelName ?? "Unknown",
                DeviceName = _currentDeviceName,
                Language = _currentLanguage,
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

        if (!string.IsNullOrWhiteSpace(_currentOutputPath))
        {
            _captureSessionStorage.SaveSessionMetadata(
                _currentOutputPath,
                new Models.CaptureSessionMetadata
                {
                    ModelName = _voskTranscriptionService?.CurrentModelName ?? "Unknown",
                    DeviceName = _currentDeviceName,
                    Language = _currentLanguage,
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
                    DateTimeOffset.Now));
        }
        finally
        {
            _pipelineGate.Release();
        }
    }

    private async Task PublishTranscriptPreviewAsync(AudioChunk chunk)
    {
        var transcription = await _transcribeAudioChunkUseCase.ExecuteAsync(
            new TranscriptionRequest(chunk, "en"));

        var partialText = transcription.Segments.LastOrDefault(segment => segment.IsPartial)?.Text ?? string.Empty;
        var finalText = transcription.Segments.LastOrDefault(segment => !segment.IsPartial)?.Text ?? string.Empty;
        var translationTarget = string.IsNullOrWhiteSpace(finalText) ? partialText : finalText;

        var translation = string.IsNullOrWhiteSpace(translationTarget)
            ? new TranslationResult(string.Empty, string.Empty, "vi")
            : await _translateTranscriptUseCase.ExecuteAsync(
                new TranslationRequest(translationTarget, "en", "vi"));

        var update = new TranscriptPreviewUpdate(
            partialText,
            string.IsNullOrWhiteSpace(finalText) ? translation.TranslatedText : string.Empty,
            finalText,
            string.IsNullOrWhiteSpace(finalText) ? string.Empty : translation.TranslatedText,
            DateTimeOffset.Now);

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
}
