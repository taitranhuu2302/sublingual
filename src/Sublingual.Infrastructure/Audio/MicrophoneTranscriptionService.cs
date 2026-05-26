using Sublingual.Domain.Audio;
using Sublingual.Domain.SpeakingPractice;
using Sublingual.Domain.Transcription;
using Sublingual.Infrastructure.Audio.Processing;
using Microsoft.Extensions.Logging;

namespace Sublingual.Infrastructure.Audio;

/// <summary>
/// Wires microphone capture → AudioFormatNormalizer → Vosk → fires FinalTranscriptReady.
/// Uses a simple energy-based VAD: accumulates Vosk partial text during speech,
/// then commits once Vosk returns a final (non-partial) result.
/// Muted automatically during AI speaking to avoid echo.
/// </summary>
public sealed class MicrophoneTranscriptionService : IMicrophoneTranscriptionService, IDisposable
{
    private readonly IAudioCaptureService _micCapture;
    private readonly ITranscriptionService _transcription;
    private readonly AudioFormatNormalizer _normalizer;
    private readonly ILogger? _logger;

    private bool _muted;
    private bool _running;
    private string _pendingText = string.Empty;

    public event EventHandler<string>? FinalTranscriptReady;

    public MicrophoneTranscriptionService(
        IAudioCaptureService micCapture,
        ITranscriptionService transcription,
        AudioFormatNormalizer normalizer,
        ILogger<MicrophoneTranscriptionService>? logger = null)
    {
        _micCapture = micCapture;
        _transcription = transcription;
        _normalizer = normalizer;
        _logger = logger;
        _micCapture.AudioChunkCaptured += OnAudioChunkCaptured;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _pendingText = string.Empty;

        _logger?.LogInformation("Microphone transcription start");

        await _micCapture.StartAsync(
            new AudioCaptureRequest(AudioSourceType.Microphone, null, 16_000, 1),
            cancellationToken
        );
    }

    public async Task StopAsync()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _pendingText = string.Empty;
        await _micCapture.StopAsync();

        _logger?.LogInformation("Microphone transcription stopped");
    }

    public void SetMuted(bool muted)
    {
        _muted = muted;
        if (muted)
        {
            _pendingText = string.Empty;
        }

        _logger?.LogDebug("Microphone muted={Muted}", muted);
    }

    public void Dispose()
    {
        _micCapture.AudioChunkCaptured -= OnAudioChunkCaptured;
        _ = StopAsync();
        (_micCapture as IDisposable)?.Dispose();
    }

    private void OnAudioChunkCaptured(object? sender, AudioChunk rawChunk)
    {
        if (_muted || !_running)
        {
            return;
        }

        // Normalize to 16kHz mono PCM16 (Vosk's expected format).
        var normalizedChunk = _normalizer.NormalizeForSpeechRecognition(rawChunk);
        if (normalizedChunk.Data.Length == 0)
        {
            return;
        }

        // Transcribe synchronously on the capture thread.
        // VoskTranscriptionService is thread-safe (uses lock internally).
        var request = new TranscriptionRequest(normalizedChunk, "en");
        TranscriptionResult result;
        try
        {
            result = _transcription.TranscribeAsync(request).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Mic STT failed");
            return;
        }

        foreach (var segment in result.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                continue;
            }

            if (segment.IsPartial)
            {
                // Vosk partial — keep showing what's being said.
                _pendingText = segment.Text;
            }
            else
            {
                // Vosk final result — fire transcript event.
                var finalText = segment.Text.Trim();
                _pendingText = string.Empty;

                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    _logger?.LogInformation("Mic STT final. Len={Len}", finalText.Length);
                    FinalTranscriptReady?.Invoke(this, finalText);
                }
            }
        }
    }
}
