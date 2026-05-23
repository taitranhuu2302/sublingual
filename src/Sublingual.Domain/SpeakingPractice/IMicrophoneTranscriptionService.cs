namespace Sublingual.Domain.SpeakingPractice;

/// <summary>
/// Captures microphone audio, runs speech recognition, and fires
/// <see cref="FinalTranscriptReady"/> when the user has finished a phrase.
/// </summary>
public interface IMicrophoneTranscriptionService
{
    /// <summary>Fires on the capture thread when a final transcript phrase is ready.</summary>
    event EventHandler<string>? FinalTranscriptReady;

    /// <summary>Start capturing and transcribing. Muted when <paramref name="muted"/> is true.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    /// <summary>While muted, audio is captured but transcripts are discarded.</summary>
    void SetMuted(bool muted);
}
