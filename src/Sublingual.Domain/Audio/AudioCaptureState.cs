namespace Sublingual.Domain.Audio;

public enum AudioCaptureState
{
    Idle = 0,
    Starting = 1,
    Capturing = 2,
    Stopping = 3,
    Faulted = 4,
}
