using System.Text.Json;
using Sublingual.Domain.Transcription;
using Vosk;

namespace Sublingual.App.Services;

public sealed class VoskTranscriptionService : ITranscriptionService, IDisposable
{
    private readonly SpeechToTextModelCatalog _modelCatalog;
    private readonly AppSettingsStore _settingsStore;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _modelLoadGate = new(1, 1);
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

    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var modelPath = ResolveModelPath();
            if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
            {
                return CreateUnavailableResult("No local speech model found.");
            }

            await EnsureModelLoadedAsync(modelPath, cancellationToken);

            if (_model is null)
            {
                return CreateUnavailableResult("Speech model could not be loaded.");
            }

            if (request.Chunk.Data.Length == 0)
            {
                return new TranscriptionResult([]);
            }

            var recognitionResult = Recognize(request.Chunk);
            return recognitionResult;
        }
        catch (Exception ex)
        {
            return CreateUnavailableResult($"Speech recognition failed: {ex.Message}");
        }
    }

    public Task PreloadModelAsync(string? modelName = null, CancellationToken cancellationToken = default)
    {
        var modelPath = ResolveModelPath(modelName);
        if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
        {
            throw new InvalidOperationException("No local speech model found.");
        }

        return EnsureModelLoadedAsync(modelPath, cancellationToken);
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

        _modelLoadGate.Dispose();
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

    private string? ResolveModelPath(string? modelName = null)
    {
        var settings = _settingsStore.Load();
        var selectedModel = string.IsNullOrWhiteSpace(modelName)
            ? settings.SpeechToText.SelectedModel
            : modelName;
        var available = _modelCatalog.GetAvailableModels();

        return available.FirstOrDefault(model =>
                string.Equals(model.Name, selectedModel, StringComparison.OrdinalIgnoreCase))?.Path
            ?? available.FirstOrDefault(model =>
                string.Equals(model.Name, "default", StringComparison.OrdinalIgnoreCase))?.Path
            ?? available.FirstOrDefault()?.Path;
    }

    private async Task EnsureModelLoadedAsync(string modelPath, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_model is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await _modelLoadGate.WaitAsync(cancellationToken);
        Model? loadedModel = null;

        try
        {
            lock (_sync)
            {
                if (_model is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            loadedModel = await Task.Run(() => new Model(modelPath), cancellationToken);

            lock (_sync)
            {
                if (_model is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    loadedModel.Dispose();
                    return;
                }

                _recognizer?.Dispose();
                _recognizer = null;
                _recognizerSampleRate = null;
                _model?.Dispose();
                _model = loadedModel;
                _loadedModelPath = modelPath;
                loadedModel = null;
            }
        }
        finally
        {
            loadedModel?.Dispose();
            _modelLoadGate.Release();
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
