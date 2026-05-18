namespace Sublingual.Domain.Transcription;

public sealed record TranscriptSegment(
    string Text,
    bool IsPartial,
    DateTimeOffset Timestamp
);
