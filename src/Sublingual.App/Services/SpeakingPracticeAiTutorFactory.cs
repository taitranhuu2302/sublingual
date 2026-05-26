using Sublingual.App.Models;
using Sublingual.Domain.SpeakingPractice;
using Sublingual.Infrastructure.AI.Gemini;
using Sublingual.Infrastructure.AI.Groq;

namespace Sublingual.App.Services;

public sealed class SpeakingPracticeAiTutorFactory : ISpeakingPracticeAiTutorFactory
{
    private readonly IReadOnlyDictionary<string, Func<SpeakingPracticeSettings, IAiTutorService>> _providers;

    public SpeakingPracticeAiTutorFactory(
        GroqSpeakingTutorService groq,
        GeminiSpeakingTutorService gemini)
    {
        _providers = new Dictionary<string, Func<SpeakingPracticeSettings, IAiTutorService>>(StringComparer.OrdinalIgnoreCase)
        {
            [SpeakingPracticeProviders.Groq] = settings =>
            {
                groq.ConfigureApiKey(settings.GroqApiKey);
                groq.ConfigureModel(settings.GroqModel);
                return groq;
            },
            [SpeakingPracticeProviders.Gemini] = settings =>
            {
                gemini.Configure(settings.GeminiApiKey, settings.GeminiModel);
                return gemini;
            },
        };
    }

    public IAiTutorService Create(SpeakingPracticeSettings settings)
    {
        var provider = settings.AiProvider ?? string.Empty;
        return _providers.TryGetValue(provider, out var factory)
            ? factory(settings)
            : _providers[SpeakingPracticeProviders.Groq](settings);
    }
}
