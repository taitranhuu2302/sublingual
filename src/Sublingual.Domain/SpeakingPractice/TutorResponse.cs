namespace Sublingual.Domain.SpeakingPractice;

/// <summary>
/// The fully parsed, structured response returned by an AI tutor model.
/// All fields are required in the LLM JSON schema — null indicates parsing failure.
/// </summary>
public sealed record TutorResponse(
    /// <summary>The AI's natural conversational reply (2-3 sentences).</summary>
    string TutorReply,

    /// <summary>
    /// Polite, constructive grammar or phrasing tip for the user's last turn.
    /// Empty string means the user's sentence was flawless.
    /// </summary>
    string EnglishEnhancement,

    /// <summary>Three suggestion options the user can choose to speak next.</summary>
    IReadOnlyList<SuggestionOption> Suggestions
);
