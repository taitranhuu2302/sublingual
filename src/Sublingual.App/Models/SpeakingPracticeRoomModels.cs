namespace Sublingual.App.Models;

public sealed class SpeakingPracticeRoomRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SpeakingPracticeMessageRecord> Messages { get; set; } = [];
    public SpeakingPracticeRoomMemoryRecord? Memory { get; set; }
}

public sealed class SpeakingPracticeMessageRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? EnhancementAdvice { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool IsSpoken { get; set; }
    public List<SpeakingPracticeSuggestionOptionRecord>? Suggestions { get; set; }
}

public sealed class SpeakingPracticeSuggestionOptionRecord
{
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class SpeakingPracticeRoomsDocument
{
    public List<SpeakingPracticeRoomRecord> Rooms { get; set; } = [];
}

public sealed class SpeakingPracticeRoomMemoryRecord
{
    public string PreferencesJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
