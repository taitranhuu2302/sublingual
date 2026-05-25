namespace Sublingual.Domain.SpeakingPractice;

public interface IAiTutorService
{
    /// <summary>
    /// Sends the conversation history and the user's latest transcribed text to the AI provider.
    /// Returns a fully structured <see cref="TutorResponse"/> with a reply, enhancement tip, and suggestions.
    /// </summary>
    Task<TutorResponse?> GetResponseAsync(
        string instructions,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Evaluates the sentence and returns ONLY the corrected version, with no context/explanations.
    /// </summary>
    Task<string> GetDirectCorrectionAsync(
        string sentence,
        CancellationToken cancellationToken = default
    );
}
