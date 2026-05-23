using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public interface IRealtimeTranslationProvider : ITranslationProvider
{
    Task<ProviderTranslationResponse?> TranslateWithMetadataAsync(
        TranslationRequest request,
        TranslationSettings settings,
        RealtimeTranslationContext? realtimeContext = null,
        CancellationToken cancellationToken = default
    );

    Task ResetSessionAsync(
        TranslationSettings settings,
        string sessionId,
        CancellationToken cancellationToken = default
    );
}
