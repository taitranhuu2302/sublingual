using System.Text.Json;
using Sublingual.Domain.Transcription;
using Vosk;

namespace Sublingual.App.Services;

public sealed class VoskTranscriptionService : ITranscriptionService, IDisposable
{
    private readonly SpeechToTextModelCatalog _modelCatalog;
    private readonly AppSettingsStore _settingsStore;
    private readonly object _sync = new();
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private string? _loadedModelPath;
    private int? _recognizerSampleRate;

    public string CurrentModelName => string.IsNullOrWhiteSpace(_loadedModelPath)
        ? "No model loaded"
        : Path.GetFileName(_loadedModelPath);

    public VoskTranscriptionService(
        SpeechToTextModelCatalog modelCatalog,
        AppSettingsStore settingsStore)
    {
        _modelCatalog = modelCatalog;
        _settingsStore = settingsStore;
        Vosk.Vosk.SetLogLevel(-1);
    }

    public Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var modelPath = ResolveModelPath();
            if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
            {
                return Task.FromResult(CreateUnavailableResult("No local speech model found."));
            }

            EnsureModelLoaded(modelPath);

            if (_model is null)
            {
                return Task.FromResult(CreateUnavailableResult("Speech model could not be loaded."));
            }

            var normalizedChunk = NormalizeChunkForRecognition(request.Chunk);
            if (normalizedChunk.Data.Length == 0)
            {
                return Task.FromResult(new TranscriptionResult([]));
            }

            var recognitionResult = Recognize(normalizedChunk);
            return Task.FromResult(recognitionResult);
        }
        catch (Exception ex)
        {
            return Task.FromResult(CreateUnavailableResult($"Speech recognition failed: {ex.Message}"));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _model?.Dispose();
            _model = null;
            _recognizer?.Dispose();
            _recognizer = null;
            _loadedModelPath = null;
            _recognizerSampleRate = null;
        }
    }

    public void ResetSession()
    {
        lock (_sync)
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _recognizerSampleRate = null;
        }
    }

    private string? ResolveModelPath()
    {
        var settings = _settingsStore.Load();
        var selectedModel = settings.SpeechToText.SelectedModel;
        var available = _modelCatalog.GetAvailableModels();

        return available.FirstOrDefault(model =>
                string.Equals(model.Name, selectedModel, StringComparison.OrdinalIgnoreCase))?.Path
            ?? available.FirstOrDefault(model =>
                string.Equals(model.Name, "default", StringComparison.OrdinalIgnoreCase))?.Path
            ?? available.FirstOrDefault()?.Path;
    }

    private void EnsureModelLoaded(string modelPath)
    {
        lock (_sync)
        {
            if (_model is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _recognizer?.Dispose();
            _recognizer = null;
            _recognizerSampleRate = null;
            _model?.Dispose();
            _model = new Model(modelPath);
            _loadedModelPath = modelPath;
        }
    }

    private TranscriptionResult Recognize(Domain.Audio.AudioChunk chunk)
    {
        lock (_sync)
        {
            if (_model is null)
            {
                return new TranscriptionResult([]);
            }

            if (_recognizer is null || _recognizerSampleRate != chunk.SampleRate)
            {
                _recognizer?.Dispose();
                _recognizer = new VoskRecognizer(_model, chunk.SampleRate);
                _recognizer.SetWords(true);
                _recognizerSampleRate = chunk.SampleRate;
            }

            var hasFinalResult = _recognizer.AcceptWaveform(chunk.Data, chunk.Data.Length);
            var json = hasFinalResult ? _recognizer.Result() : _recognizer.PartialResult();
            var text = hasFinalResult ? ExtractText(json) : ExtractPartialText(json);

            if (string.IsNullOrWhiteSpace(text))
            {
                return new TranscriptionResult([]);
            }

            IReadOnlyList<TranscriptSegment> segments =
            [
                new TranscriptSegment(text.Trim(), !hasFinalResult, DateTimeOffset.Now),
            ];

            return new TranscriptionResult(segments);
        }
    }

    private static Domain.Audio.AudioChunk NormalizeChunkForRecognition(Domain.Audio.AudioChunk chunk)
    {
        if (chunk.BitsPerSample == 16 && chunk.Channels == 1)
        {
            return chunk;
        }

        if (chunk.BitsPerSample == 32)
        {
            return ConvertFloatChunkToPcm16Mono(chunk);
        }

        if (chunk.BitsPerSample == 16 && chunk.Channels > 1)
        {
            return DownmixPcm16ChunkToMono(chunk);
        }

        return chunk;
    }

    private static Domain.Audio.AudioChunk ConvertFloatChunkToPcm16Mono(Domain.Audio.AudioChunk chunk)
    {
        var frameCount = chunk.Data.Length / sizeof(float) / Math.Max(1, chunk.Channels);
        var output = new byte[frameCount * sizeof(short)];

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            double sum = 0;
            for (var channelIndex = 0; channelIndex < chunk.Channels; channelIndex++)
            {
                var sampleOffset = (frameIndex * chunk.Channels + channelIndex) * sizeof(float);
                sum += BitConverter.ToSingle(chunk.Data, sampleOffset);
            }

            var monoSample = (float)(sum / chunk.Channels);
            var pcm16Sample = (short)Math.Clamp(monoSample * short.MaxValue, short.MinValue, short.MaxValue);
            var outputOffset = frameIndex * sizeof(short);
            output[outputOffset] = (byte)(pcm16Sample & 0xFF);
            output[outputOffset + 1] = (byte)((pcm16Sample >> 8) & 0xFF);
        }

        return new Domain.Audio.AudioChunk(output, chunk.SampleRate, 1, 16, chunk.Duration, chunk.CapturedAt);
    }

    private static Domain.Audio.AudioChunk DownmixPcm16ChunkToMono(Domain.Audio.AudioChunk chunk)
    {
        var frameCount = chunk.Data.Length / sizeof(short) / Math.Max(1, chunk.Channels);
        var output = new byte[frameCount * sizeof(short)];

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            int sum = 0;
            for (var channelIndex = 0; channelIndex < chunk.Channels; channelIndex++)
            {
                var sampleOffset = (frameIndex * chunk.Channels + channelIndex) * sizeof(short);
                sum += BitConverter.ToInt16(chunk.Data, sampleOffset);
            }

            var pcm16Sample = (short)(sum / chunk.Channels);
            var outputOffset = frameIndex * sizeof(short);
            output[outputOffset] = (byte)(pcm16Sample & 0xFF);
            output[outputOffset + 1] = (byte)((pcm16Sample >> 8) & 0xFF);
        }

        return new Domain.Audio.AudioChunk(output, chunk.SampleRate, 1, 16, chunk.Duration, chunk.CapturedAt);
    }

    private static string ExtractText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("text", out var textElement))
        {
            return textElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ExtractPartialText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("partial", out var textElement))
        {
            return textElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static TranscriptionResult CreateUnavailableResult(string message)
    {
        IReadOnlyList<TranscriptSegment> segments =
        [
            new TranscriptSegment(message, false, DateTimeOffset.Now),
        ];

        return new TranscriptionResult(segments);
    }
}
