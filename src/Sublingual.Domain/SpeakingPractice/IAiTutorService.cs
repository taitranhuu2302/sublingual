namespace Sublingual.Domain.SpeakingPractice;

public interface IAiTutorService
{
    /// <summary>
    /// Sends the conversation history and the user's latest transcribed text to the AI provider.
    /// Returns a fully structured <see cref="TutorResponse"/> with a reply, enhancement tip, and suggestions.
    /// </summary>
    Task<TutorResponse?> GetResponseAsync(
        string topic,
        string languageLevel,
        IReadOnlyList<PracticeMessage> history,
        string userText,
        CancellationToken cancellationToken = default
    );
}
