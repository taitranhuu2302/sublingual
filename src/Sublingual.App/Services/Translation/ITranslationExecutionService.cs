using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public interface ITranslationExecutionService : ITranslationService
{
    Task<TranslationExecutionResult> TranslateWithDiagnosticsAsync(
        TranslationRequest request,
        RealtimeTranslationContext? realtimeContext = null,
        CancellationToken cancellationToken = default
    );

    void ClearCache();
}
