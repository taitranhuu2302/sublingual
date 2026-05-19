using Sublingual.Domain.Transcription;

namespace Sublingual.App.Services;

public sealed class MockTranscriptionService : ITranscriptionService
{
    private static readonly string[] SampleLines =
    [
        "System audio capture is active and chunking in real time.",
        "This preview simulates a partial subtitle before the final line settles.",
        "The overlay window is now fed by the same shared capture session.",
        "Next milestone is replacing mock transcription with a real provider."
    ];

    private int _index;

    public Task<TranscriptionResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = SampleLines[_index % SampleLines.Length];
        _index += 1;

        var partialLength = Math.Max(12, text.Length / 2);
        IReadOnlyList<TranscriptSegment> segments =
        [
            new TranscriptSegment(text[..partialLength], true, DateTimeOffset.Now),
            new TranscriptSegment(text, false, DateTimeOffset.Now),
        ];

        return Task.FromResult(new TranscriptionResult(segments));
    }
}
