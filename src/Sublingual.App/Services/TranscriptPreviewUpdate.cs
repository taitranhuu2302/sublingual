namespace Sublingual.App.Services;

public sealed record TranscriptPreviewUpdate(
    string PartialText,
    string PartialTranslatedText,
    string FinalText,
    string FinalTranslatedText,
    DateTimeOffset UpdatedAt,
    string TranslationProvider,
    string TranslationDiagnostics,
    bool TranslationCacheHit);
