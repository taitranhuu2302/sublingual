using Sublingual.Domain.Audio;

namespace Sublingual.App.Services;

internal sealed class DesignTimeAudioCaptureService : IAudioCaptureService
{
    public static DesignTimeAudioCaptureService Instance { get; } = new();

    private DesignTimeAudioCaptureService()
    {
    }

    public AudioCaptureState State => AudioCaptureState.Idle;

#pragma warning disable CS0067
    public event EventHandler<AudioChunk>? AudioChunkCaptured;
#pragma warning restore CS0067

    public Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(
        AudioSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AudioDevice> devices =
        [
            new AudioDevice("default-device", "Headphones (Design Preview)", true),
            new AudioDevice("secondary-device", "Speakers (Design Preview)", false),
        ];

        return Task.FromResult(devices);
    }

    public Task StartAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
