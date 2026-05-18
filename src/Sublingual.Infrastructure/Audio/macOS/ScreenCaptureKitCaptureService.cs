using System.Runtime.InteropServices;
using Sublingual.Domain.Audio;
using Sublingual.Interop.macOS;

namespace Sublingual.Infrastructure.Audio.macOS;

public sealed class ScreenCaptureKitCaptureService : IAudioCaptureService, IDisposable
{
    private readonly AudioBufferCallback _audioBufferCallback;
    private GCHandle? _callbackHandle;
    private AudioCaptureState _state = AudioCaptureState.Idle;

    public ScreenCaptureKitCaptureService()
    {
        _audioBufferCallback = OnAudioBuffer;
    }

    public AudioCaptureState State => _state;

    public event EventHandler<AudioChunk>? AudioChunkCaptured;

    public Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(
        AudioSourceType sourceType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceType != AudioSourceType.System || !OperatingSystem.IsMacOS())
        {
            return Task.FromResult<IReadOnlyList<AudioDevice>>([]);
        }

        IReadOnlyList<AudioDevice> devices =
        [
            new AudioDevice(
                "macos-system-default",
                "System Audio (ScreenCaptureKit)",
                true
            ),
        ];

        return Task.FromResult(devices);
    }

    public Task StartAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "ScreenCaptureKit capture is only supported on macOS."
            );
        }

        if (request.SourceType != AudioSourceType.System)
        {
            throw new NotSupportedException(
                "ScreenCaptureKitCaptureService only supports system audio capture."
            );
        }

        _callbackHandle ??= GCHandle.Alloc(this);
        var createStatus = ScreenCaptureKitNative.CreateSession(
            _audioBufferCallback,
            GCHandle.ToIntPtr(_callbackHandle.Value)
        );

        if (createStatus != 0)
        {
            _state = AudioCaptureState.Faulted;
            throw new InvalidOperationException(ScreenCaptureKitNative.GetLastErrorMessage());
        }

        _state = AudioCaptureState.Starting;
        var startStatus = ScreenCaptureKitNative.StartCapture();
        if (startStatus != 0)
        {
            _state = AudioCaptureState.Faulted;
            throw new InvalidOperationException(ScreenCaptureKitNative.GetLastErrorMessage());
        }

        _state = AudioCaptureState.Capturing;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsMacOS())
        {
            _state = AudioCaptureState.Idle;
            return Task.CompletedTask;
        }

        _state = AudioCaptureState.Stopping;
        ScreenCaptureKitNative.StopCapture();
        ScreenCaptureKitNative.DestroySession();

        if (_callbackHandle is { } handle)
        {
            handle.Free();
            _callbackHandle = null;
        }

        _state = AudioCaptureState.Idle;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _ = StopAsync();
    }

    private void OnAudioBuffer(
        IntPtr samples,
        int frameCount,
        int channels,
        double timestamp,
        IntPtr context
    )
    {
        if (samples == IntPtr.Zero || frameCount <= 0 || channels <= 0)
        {
            return;
        }

        var sampleCount = frameCount * channels;
        var floatSamples = new float[sampleCount];
        Marshal.Copy(samples, floatSamples, 0, sampleCount);

        var buffer = new byte[sampleCount * sizeof(float)];
        Buffer.BlockCopy(floatSamples, 0, buffer, 0, buffer.Length);

        var duration = TimeSpan.FromSeconds(frameCount / 48_000d);
        AudioChunkCaptured?.Invoke(
            this,
            new AudioChunk(
                buffer,
                48_000,
                channels,
                32,
                duration,
                DateTimeOffset.FromUnixTimeMilliseconds((long)(timestamp * 1000))
            )
        );
    }
}
