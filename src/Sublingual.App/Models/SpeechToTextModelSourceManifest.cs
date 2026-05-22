namespace Sublingual.App.Models;

public sealed class SpeechToTextModelSourceManifest
{
    public List<SpeechToTextDefaultModelSource> DefaultModels { get; set; } = [];
}

public sealed class SpeechToTextDefaultModelSource
{
    public string Language { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ZipUrl { get; set; } = string.Empty;
}
