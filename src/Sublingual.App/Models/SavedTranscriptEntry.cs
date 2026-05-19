namespace Sublingual.App.Models;

public sealed class SavedTranscriptEntry
{
    public required string PartialText { get; init; }
    public required string PartialTranslatedText { get; init; }
    public required string FinalText { get; init; }
    public required string FinalTranslatedText { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
