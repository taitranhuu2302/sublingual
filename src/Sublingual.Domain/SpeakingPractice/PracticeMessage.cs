namespace Sublingual.Domain.SpeakingPractice;

public sealed record PracticeMessage(
    string Id,
    MessageSender Sender,
    string Text,
    string? EnhancementAdvice,
    DateTimeOffset Timestamp,
    System.Collections.Generic.IReadOnlyList<SuggestionOption>? Suggestions = null
);

public enum MessageSender
{
    User,
    Ai,
}
