namespace Sublingual.Domain.Audio;

public interface IAudioCaptureService
{
    AudioCaptureState State { get; }

    event EventHandler<AudioChunk>? AudioChunkCaptured;

    Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(
        AudioSourceType sourceType,
        CancellationToken cancellationToken = default
    );

    Task StartAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken = default
    );

    Task StopAsync(CancellationToken cancellationToken = default);
}
