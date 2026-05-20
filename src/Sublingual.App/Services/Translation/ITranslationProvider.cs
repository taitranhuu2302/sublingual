using Sublingual.App.Models;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services.Translation;

public interface ITranslationProvider
{
    string Name { get; }

    bool IsEnabled(TranslationSettings settings);

    Task<TranslationResult?> TranslateAsync(
        TranslationRequest request,
        TranslationSettings settings,
        CancellationToken cancellationToken = default
    );
}
