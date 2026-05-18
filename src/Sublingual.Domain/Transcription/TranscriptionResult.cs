namespace Sublingual.Domain.Transcription;

public sealed record TranscriptionResult(
    IReadOnlyList<TranscriptSegment> Segments
);
