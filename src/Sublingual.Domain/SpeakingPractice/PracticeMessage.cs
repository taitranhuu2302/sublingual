namespace Sublingual.Domain.SpeakingPractice;

public sealed record PracticeMessage(
    string Id,
    MessageSender Sender,
    string Text,
    string? EnhancementAdvice,
    DateTimeOffset Timestamp
);

public enum MessageSender
{
    User,
    Ai,
}
