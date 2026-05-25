using Sublingual.App.Models;
using Sublingual.Domain.SpeakingPractice;
using Sublingual.Infrastructure.AI.Gemini;
using Sublingual.Infrastructure.AI.Groq;

namespace Sublingual.App.Services;

public sealed class SpeakingPracticeDynamicAiTutorService : IAiTutorService
{
    private readonly AppSettingsStore _settingsStore;
    private readonly GroqSpeakingTutorService _groq;
    private readonly GeminiSpeakingTutorService _gemini;

    public SpeakingPracticeDynamicAiTutorService(
        AppSettingsStore settingsStore,
        GroqSpeakingTutorService groq,
        GeminiSpeakingTutorService gemini)
    {
        _settingsStore = settingsStore;
        _groq = groq;
        _gemini = gemini;
    }

    public Task<TutorResponse?> GetResponseAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsStore.Load().SpeakingPractice;
        var provider = string.Equals(settings.AiProvider, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase)
            ? SpeakingPracticeProviders.Gemini
            : SpeakingPracticeProviders.Groq;

        if (string.Equals(provider, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase))
        {
            _gemini.Configure(settings.GeminiApiKey, settings.GeminiModel);
            return _gemini.GetResponseAsync(instructions, languageLevel, history, cancellationToken);
        }

        _groq.ConfigureApiKey(settings.GroqApiKey);
        _groq.ConfigureModel(settings.GroqModel);
        return _groq.GetResponseAsync(instructions, languageLevel, history, cancellationToken);
    }

    public Task<string> GetDirectCorrectionAsync(
        string sentence,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsStore.Load().SpeakingPractice;
        var provider = string.Equals(settings.AiProvider, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase)
            ? SpeakingPracticeProviders.Gemini
            : SpeakingPracticeProviders.Groq;

        if (string.Equals(provider, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase))
        {
            _gemini.Configure(settings.GeminiApiKey, settings.GeminiModel);
            return _gemini.GetDirectCorrectionAsync(sentence, cancellationToken);
        }

        _groq.ConfigureApiKey(settings.GroqApiKey);
        _groq.ConfigureModel(settings.GroqModel);
        return _groq.GetDirectCorrectionAsync(sentence, cancellationToken);
    }
}
