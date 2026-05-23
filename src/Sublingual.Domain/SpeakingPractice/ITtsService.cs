namespace Sublingual.Domain.SpeakingPractice;

public interface ITtsService
{
    /// <summary>Returns true if the service is currently speaking.</summary>
    bool IsSpeaking { get; }

    /// <summary>
    /// Speaks the given text asynchronously.
    /// Cancelling the token stops playback immediately.
    /// </summary>
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Immediately stops any ongoing speech.</summary>
    void StopSpeaking();
}
