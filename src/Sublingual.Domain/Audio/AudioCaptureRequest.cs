namespace Sublingual.Domain.Audio;

public sealed record AudioCaptureRequest(
    AudioSourceType SourceType,
    string? DeviceId,
    int TargetSampleRate,
    int TargetChannels
);
