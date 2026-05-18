namespace Sublingual.Domain.Audio;

public sealed record AudioChunk(
    byte[] Data,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    TimeSpan Duration,
    DateTimeOffset CapturedAt
);
