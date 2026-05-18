namespace Sublingual.Domain.Transcription;

public sealed record TranslationRequest(
    string SourceText,
    string SourceLanguage,
    string TargetLanguage
);
