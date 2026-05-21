namespace Sublingual.App.Services;

public abstract record RealtimeTranscriptEvent(
    long SequenceId,
    DateTimeOffset UpdatedAt
);

public sealed record DraftTranscriptChanged(
    long SequenceId,
    string DraftSegmentId,
    string OriginalText,
    DateTimeOffset UpdatedAt
) : RealtimeTranscriptEvent(SequenceId, UpdatedAt);

public sealed record StableTranscriptCommitted(
    long SequenceId,
    string SegmentId,
    string OriginalText,
    DateTimeOffset UpdatedAt
) : RealtimeTranscriptEvent(SequenceId, UpdatedAt);

public sealed record TranscriptTranslationChanged(
    long SequenceId,
    string SegmentId,
    TranscriptTranslationTarget Target,
    string SourceText,
    string TranslatedText,
    bool IsPending,
    string ProviderName,
    bool IsCacheHit,
    DateTimeOffset UpdatedAt
) : RealtimeTranscriptEvent(SequenceId, UpdatedAt);

public enum TranscriptTranslationTarget
{
    Draft = 0,
    StableSegment = 1,
}
