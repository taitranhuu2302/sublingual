using Sublingual.Domain.Transcription;

namespace Sublingual.Application.Audio;

public sealed class TranslateTranscriptUseCase(
    ITranslationService translationService
)
{
    public Task<TranslationResult> ExecuteAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default
    ) => translationService.TranslateAsync(request, cancellationToken);
}
