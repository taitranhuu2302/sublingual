namespace Sublingual.Domain.Transcription;

public sealed record TranslationResult(
    string SourceText,
    string TranslatedText,
    string TargetLanguage
);
