namespace Sublingual.Domain.Transcription;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default
    );
}
