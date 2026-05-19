using System.Collections.Generic;
using System.Linq;
using Sublingual.App.Models;

namespace Sublingual.App.Services;

public sealed class SpeechToTextModelCatalog
{
    private readonly string _bundledModelsRoot;
    private readonly string _managedModelsRoot;

    public SpeechToTextModelCatalog()
    {
        _bundledModelsRoot = Path.Combine(AppContext.BaseDirectory, "speech-to-text-models");

        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _managedModelsRoot = Path.Combine(appDataRoot, "Sublingual", "speech-to-text-models");
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
        Directory.CreateDirectory(_managedModelsRoot);
        return _managedModelsRoot;
    }

    private IEnumerable<string> GetModelRoots()
    {
        yield return _managedModelsRoot;
        yield return _bundledModelsRoot;
    }
}
