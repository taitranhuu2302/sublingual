using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed record TranslationExecutionResult(
    TranslationResult Result,
    string ProviderName,
    IReadOnlyList<string> AttemptLog,
    bool IsCacheHit
);
