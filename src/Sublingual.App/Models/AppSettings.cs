namespace Sublingual.App.Models;

public sealed class AppSettings
{
    public OverlaySettings Overlay { get; set; } = new();
    public SpeechToTextSettings SpeechToText { get; set; } = new();
}

public sealed class OverlaySettings
{
    public double FontSize { get; set; } = 26;
    public double LineHeight { get; set; } = 1.35;
    public double Width { get; set; } = 720;
    public double Height { get; set; } = 200;
    public string Theme { get; set; } = "Dark";
    public double Opacity { get; set; } = 0.88;
    public bool ShowTranslation { get; set; } = true;
    public int? PositionX { get; set; }
    public int? PositionY { get; set; }
}

public sealed class SpeechToTextSettings
{
    public string SelectedModel { get; set; } = "default";
}
