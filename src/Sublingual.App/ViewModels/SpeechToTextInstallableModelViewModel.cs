namespace Sublingual.App.ViewModels;

public sealed class SpeechToTextInstallableModelViewModel
{
    public required string Language { get; init; }
    public required string ModelName { get; init; }
    public required string DisplayName { get; init; }
    public required string ZipUrl { get; init; }
    public bool IsInstalled { get; set; }
    public bool CanInstall => !IsInstalled && !string.IsNullOrWhiteSpace(ZipUrl);
    public string InstallButtonLabel => IsInstalled ? "Installed" : "Install";
    public string Summary => string.IsNullOrWhiteSpace(Language)
        ? ModelName
        : $"{DisplayName} ({Language})";
}
