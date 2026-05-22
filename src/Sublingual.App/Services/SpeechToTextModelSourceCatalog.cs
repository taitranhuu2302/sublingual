using System.Text.Json;
using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class SpeechToTextModelSourceCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _manifestPath = Path.Combine(AppContext.BaseDirectory, "Config", "speech-to-text-model-sources.json");

    public SpeechToTextDefaultModelSource? GetDefaultModel(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var manifest = LoadManifest();
        return manifest.DefaultModels.FirstOrDefault(model =>
            string.Equals(model.Language, language, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<SpeechToTextDefaultModelSource> GetDefaultModels()
    {
        return LoadManifest().DefaultModels;
    }

    private SpeechToTextModelSourceManifest LoadManifest()
    {
        try
        {
            if (!File.Exists(_manifestPath))
            {
                return new SpeechToTextModelSourceManifest();
            }

            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<SpeechToTextModelSourceManifest>(json, SerializerOptions)
                ?? new SpeechToTextModelSourceManifest();
        }
        catch
        {
            return new SpeechToTextModelSourceManifest();
        }
    }
}
