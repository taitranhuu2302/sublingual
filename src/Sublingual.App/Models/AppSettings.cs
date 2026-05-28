namespace Sublingual.App.Models;

public sealed class AppSettings
{
    public StorageSettings Storage { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public SpeechToTextSettings SpeechToText { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
}

public sealed class UiSettings
{
    public bool HasSeenTrayExitHint { get; set; }
}

public sealed class StorageSettings
{
    public string SessionsRoot { get; set; } = "sessions";
    public string SpeechToTextModelsRoot { get; set; } = "speech-to-text-models";
    public string LastSessionFolderId { get; set; } = string.Empty;
    public string LastSessionTreePath { get; set; } = string.Empty;
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
    public string RealtimeChunkPreset { get; set; } = "Balanced";
}

public sealed class TranslationSettings
{
    public string Factory { get; set; } = TranslationFactories.FallbackChain;
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "vi";
    public bool TranslatePartials { get; set; }
    public List<string> ProviderOrder { get; set; } =
    [
        TranslationProviders.TranslateServiceLocal,
        TranslationProviders.GoogleTranslateFreeApi,
        TranslationProviders.LibreTranslate,
    ];
    public TranslateServiceLocalSettings TranslateServiceLocal { get; set; } = new();
    public GoogleTranslateFreeApiSettings GoogleTranslateFreeApi { get; set; } = new();
    public LibreTranslateSettings LibreTranslate { get; set; } = new();
}

public sealed class TranslateServiceLocalSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://127.0.0.1:3333";
    public bool UseRealtimeEndpointForFinals { get; set; } = true;
}

public sealed class GoogleTranslateFreeApiSettings
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "https://translate.googleapis.com/translate_a/single";
}

public sealed class LibreTranslateSettings
{
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "https://libretranslate.com/translate";
    public string ApiKey { get; set; } = string.Empty;
}

public static class TranslationFactories
{
    public const string FallbackChain = "FallbackChain";
}

public static class TranslationProviders
{
    public const string TranslateServiceLocal = "TranslateServiceLocal";
    public const string GoogleTranslateFreeApi = "GoogleTranslateFreeApi";
    public const string LibreTranslate = "LibreTranslate";
}
