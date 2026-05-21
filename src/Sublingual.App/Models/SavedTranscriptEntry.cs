using System.Text.Json.Serialization;

namespace Sublingual.App.Models;

public sealed class SavedTranscriptEntry
{
    public required string SegmentId { get; init; }
    public required string OriginalText { get; init; }
    public required string TranslatedText { get; init; }
    public required bool IsFinal { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    [JsonIgnore]
    public string PartialText => IsFinal ? string.Empty : OriginalText;

    [JsonIgnore]
    public string PartialTranslatedText => IsFinal ? string.Empty : TranslatedText;

    [JsonIgnore]
    public string FinalText => IsFinal ? OriginalText : string.Empty;

    [JsonIgnore]
    public string FinalTranslatedText => IsFinal ? TranslatedText : string.Empty;
}
