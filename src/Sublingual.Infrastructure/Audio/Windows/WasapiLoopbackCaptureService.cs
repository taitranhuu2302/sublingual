using NAudio.CoreAudioApi;
using NAudio.Wave;
using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Windows;

public sealed class WasapiLoopbackCaptureService : IAudioCaptureService, IDisposable
{
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private WasapiLoopbackCapture? _capture;
    private AudioCaptureState _state = AudioCaptureState.Idle;

    public AudioCaptureState State => _state;

    public event EventHandler<AudioChunk>? AudioChunkCaptured;

    public Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(
        AudioSourceType sourceType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (sourceType != AudioSourceType.System || !OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<AudioDevice>>([]);
        }

        var defaultDeviceId = _deviceEnumerator.GetDefaultAudioEndpoint(
            DataFlow.Render,
            Role.Multimedia
        ).ID;

        var devices = _deviceEnumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device => new AudioDevice(device.ID, device.FriendlyName, device.ID == defaultDeviceId))
            .ToList();

        return Task.FromResult<IReadOnlyList<AudioDevice>>(devices);
    }

    public Task StartAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "WASAPI loopback capture is only supported on Windows."
            );
        }

        if (request.SourceType != AudioSourceType.System)
        {
            throw new NotSupportedException(
                "WasapiLoopbackCaptureService only supports system audio capture."
            );
        }

        if (_capture is not null)
        {
            return Task.CompletedTask;
        }

        _state = AudioCaptureState.Starting;

        var selectedDevice = string.IsNullOrWhiteSpace(request.DeviceId)
            ? _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : _deviceEnumerator.GetDevice(request.DeviceId);

        _capture = new WasapiLoopbackCapture(selectedDevice);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();

        _state = AudioCaptureState.Capturing;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_capture is null)
        {
            _state = AudioCaptureState.Idle;
            return Task.CompletedTask;
        }

        _state = AudioCaptureState.Stopping;
        _capture.StopRecording();
        CleanupCapture();
        _state = AudioCaptureState.Idle;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        CleanupCapture();
        _deviceEnumerator.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var data = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesRecorded);

        var waveFormat = _capture.WaveFormat;
        var bytesPerSecond = Math.Max(1, waveFormat.AverageBytesPerSecond);
        var duration = TimeSpan.FromSeconds((double)e.BytesRecorded / bytesPerSecond);

        AudioChunkCaptured?.Invoke(
            this,
            new AudioChunk(
                data,
                waveFormat.SampleRate,
                waveFormat.Channels,
                waveFormat.BitsPerSample,
                duration,
                DateTimeOffset.UtcNow
            )
        );
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _state = e.Exception is null ? AudioCaptureState.Idle : AudioCaptureState.Faulted;
        CleanupCapture();
    }

    private void CleanupCapture()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
    }
}
