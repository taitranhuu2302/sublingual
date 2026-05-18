namespace Sublingual.Domain.Sessions;

public enum SessionState
{
    Idle = 0,
    Starting = 1,
    Capturing = 2,
    Processing = 3,
    Error = 4,
    Stopped = 5,
}
