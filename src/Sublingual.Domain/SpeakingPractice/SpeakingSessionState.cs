namespace Sublingual.Domain.SpeakingPractice;

public enum SpeakingSessionState
{
    Idle,
    Listening,
    Transcribing,
    AiThinking,
    AiSpeaking,
    Paused,
}
