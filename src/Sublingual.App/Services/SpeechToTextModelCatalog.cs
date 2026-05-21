using System.Collections.Generic;
using System.Linq;
using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class SpeechToTextModelCatalog
{
    private readonly string _bundledModelsRoot;
    private readonly AppSettingsStore _settingsStore;

    public SpeechToTextModelCatalog(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _bundledModelsRoot = Path.Combine(AppContext.BaseDirectory, "speech-to-text-models");
    }

    public IReadOnlyList<SpeechToTextModelOption> GetAvailableModels()
    {
        return GetModelRoots()
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Select(path => new SpeechToTextModelOption
            {
                Name = Path.GetFileName(path),
                Path = path,
            })
            .GroupBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string GetManagedModelsRoot()
    {
        var settings = _settingsStore.Load();
        var managedModelsRoot = AppPathHelper.ResolveConfiguredPath(
            settings.Storage.SpeechToTextModelsRoot,
            "speech-to-text-models"
        );

        Directory.CreateDirectory(managedModelsRoot);
        return managedModelsRoot;
    }

    private IEnumerable<string> GetModelRoots()
    {
        yield return GetManagedModelsRoot();
        yield return _bundledModelsRoot;
    }
}
