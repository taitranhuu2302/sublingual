using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services;

public sealed class MockTranslationService : ITranslationService
{
    public Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var translated = $"VI preview: {request.SourceText}";
        return Task.FromResult(new TranslationResult(request.SourceText, translated, request.TargetLanguage));
    }
}
