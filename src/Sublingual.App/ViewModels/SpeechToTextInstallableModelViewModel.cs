namespace Sublingual.App.ViewModels;

public sealed class SpeechToTextInstallableModelViewModel
{
    public required string Language { get; init; }
    public required string ModelName { get; init; }
    public required string DisplayName { get; init; }
    public required string ZipUrl { get; init; }
    public bool IsInstalled { get; set; }
    public bool IsDownloading { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanInstall => !IsInstalled && !IsDownloading && !string.IsNullOrWhiteSpace(ZipUrl);
    public string InstallButtonLabel => IsInstalled ? "Installed" : IsDownloading ? "Downloading..." : HasError ? "Retry" : "Install";
    public string Summary => string.IsNullOrWhiteSpace(Language)
        ? ModelName
        : $"{DisplayName} ({Language})";
}
