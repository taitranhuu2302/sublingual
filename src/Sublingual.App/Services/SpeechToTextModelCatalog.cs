using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class SpeechToTextModelCatalog
{
    private readonly string _modelsRoot;

    public SpeechToTextModelCatalog()
    {
        _modelsRoot = Path.Combine(AppContext.BaseDirectory, "speech-to-text-models");
    }

    public IReadOnlyList<SpeechToTextModelOption> GetAvailableModels()
    {
        if (!Directory.Exists(_modelsRoot))
        {
            return [];
        }

        return Directory
            .GetDirectories(_modelsRoot)
            .Select(path => new SpeechToTextModelOption
            {
                Name = Path.GetFileName(path),
                Path = path,
            })
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
