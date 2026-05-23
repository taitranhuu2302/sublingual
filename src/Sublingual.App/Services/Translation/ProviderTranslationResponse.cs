using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public sealed record ProviderTranslationResponse(
    TranslationResult Result,
    IReadOnlyList<string> Diagnostics,
    bool IsCacheHit = false
);
