namespace Sublingual.App.Services.Translation;

public sealed record RealtimeTranslationContext(
    string SessionId,
    string SegmentId,
    long SequenceId,
    TranscriptTranslationTarget Target,
    bool IsFinal
)
{
    public bool UseQualityModel =>
        Target == TranscriptTranslationTarget.StableSegment;
}
