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
    private string? _loadedModelPath;

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

            using var recognizer = new VoskRecognizer(_model, request.Chunk.SampleRate);
            recognizer.SetWords(true);

            recognizer.AcceptWaveform(request.Chunk.Data, request.Chunk.Data.Length);

            var finalJson = recognizer.FinalResult();
            var text = ExtractText(finalJson);

            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(new TranscriptionResult([]));
            }

            IReadOnlyList<TranscriptSegment> segments =
            [
                new TranscriptSegment(text.Trim(), false, DateTimeOffset.Now),
            ];

            return Task.FromResult(new TranscriptionResult(segments));
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
            _loadedModelPath = null;
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

            _model?.Dispose();
            _model = new Model(modelPath);
            _loadedModelPath = modelPath;
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

    private static TranscriptionResult CreateUnavailableResult(string message)
    {
        IReadOnlyList<TranscriptSegment> segments =
        [
            new TranscriptSegment(message, false, DateTimeOffset.Now),
        ];

        return new TranscriptionResult(segments);
    }
}
