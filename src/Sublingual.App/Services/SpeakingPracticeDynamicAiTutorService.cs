using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.App.Services;

public sealed class SpeakingPracticeDynamicAiTutorService : IAiTutorService
{
    private readonly AppSettingsStore _settingsStore;
    private readonly ISpeakingPracticeAiTutorFactory _aiTutorFactory;

    public SpeakingPracticeDynamicAiTutorService(
        AppSettingsStore settingsStore,
        ISpeakingPracticeAiTutorFactory aiTutorFactory)
    {
        _settingsStore = settingsStore;
        _aiTutorFactory = aiTutorFactory;
    }

    public Task<TutorResponse?> GetResponseAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsStore.Load().SpeakingPractice;
        var tutor = _aiTutorFactory.Create(settings);
        return tutor.GetResponseAsync(instructions, languageLevel, history, cancellationToken);
    }

    public Task<string> GetDirectCorrectionAsync(
        string sentence,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsStore.Load().SpeakingPractice;
        var tutor = _aiTutorFactory.Create(settings);
        return tutor.GetDirectCorrectionAsync(sentence, cancellationToken);
    }
}
