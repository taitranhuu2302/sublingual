namespace Sublingual.Domain.Audio;

public sealed record AudioDevice(
    string Id,
    string Name,
    bool IsDefault
);
