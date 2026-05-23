using System.Runtime.InteropServices;
using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.macOS;

/// <summary>
/// Captures microphone audio on macOS using AudioToolbox AudioQueue APIs via P/Invoke.
/// No additional dylib required — AudioToolbox is a system framework on every Mac.
/// Produces 16kHz mono PCM16 directly (matching Vosk's expected input format).
/// </summary>
public sealed class CoreAudioMicrophoneCaptureService : IAudioCaptureService, IDisposable
{
    // ── AudioQueue P/Invoke ───────────────────────────────────────────────────

    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const int kAudioFormatLinearPCM = 0x6C70636D; // 'lpcm'
    private const int kLinearPCMFormatFlagIsSignedInteger = 0x4;
    private const int kLinearPCMFormatFlagIsPacked = 0x8;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AudioQueueInputCallback(
        IntPtr inUserData,
        IntPtr inAQ,
        IntPtr inBuffer,
        ref AudioTimeStamp inStartTime,
        uint inNumberPacketDescriptions,
        IntPtr inPacketDescs);

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double mSampleRate;
        public uint mFormatID;
        public uint mFormatFlags;
        public uint mBytesPerPacket;
        public uint mFramesPerPacket;
        public uint mBytesPerFrame;
        public uint mChannelsPerFrame;
        public uint mBitsPerChannel;
        public uint mReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioTimeStamp
    {
        public double mSampleTime;
        public ulong mHostTime;
        public double mRateScalar;
        public ulong mWordClockTime;
        public uint mSMPTETime;
        public uint mFlags;
        public uint mReserved;
    }

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueNewInput(
        ref AudioStreamBasicDescription inFormat,
        AudioQueueInputCallback inCallbackProc,
        IntPtr inUserData,
        IntPtr inCallbackRunLoop,
        IntPtr inCallbackRunLoopMode,
        uint inFlags,
        out IntPtr outAQ);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueAllocateBuffer(IntPtr inAQ, uint inBufferByteSize, out IntPtr outBuffer);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueEnqueueBuffer(
        IntPtr inAQ, IntPtr inBuffer, uint inNumPacketDescs, IntPtr inPacketDescs);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueStart(IntPtr inAQ, IntPtr inStartTime);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueStop(IntPtr inAQ, bool inImmediate);

    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueDispose(IntPtr inAQ, bool inImmediate);

    // AudioQueueBuffer layout: mAudioDataBytesCapacity(4), mAudioData(ptr), mAudioDataByteSize(4), ...
    [DllImport(AudioToolbox, CallingConvention = CallingConvention.Cdecl)]
    private static extern int AudioQueueGetProperty(IntPtr inAQ, uint inID, IntPtr outData, ref uint ioDataSize);

    // ── Fields ────────────────────────────────────────────────────────────────

    private const int SampleRate = 16_000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const int BufferSize = 8_192; // ~256ms at 16kHz 16bit mono
    private const int BufferCount = 3;

    private IntPtr _audioQueue = IntPtr.Zero;
    private readonly IntPtr[] _buffers = new IntPtr[BufferCount];
    private GCHandle _selfHandle;
    private AudioQueueInputCallback? _callbackDelegate; // keep ref so GC doesn't collect it
    private AudioCaptureState _state = AudioCaptureState.Idle;

    public AudioCaptureState State => _state;
    public event EventHandler<AudioChunk>? AudioChunkCaptured;

    public Task<IReadOnlyList<AudioDevice>> GetAvailableDevicesAsync(
        AudioSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        if (sourceType != AudioSourceType.Microphone || !OperatingSystem.IsMacOS())
        {
            return Task.FromResult<IReadOnlyList<AudioDevice>>([]);
        }

        IReadOnlyList<AudioDevice> devices =
        [
            new AudioDevice("macos-default-mic", "Default Microphone", true),
        ];
        return Task.FromResult(devices);
    }

    public Task StartAsync(AudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("CoreAudio microphone capture is only supported on macOS.");
        }

        if (request.SourceType != AudioSourceType.Microphone)
        {
            throw new NotSupportedException("CoreAudioMicrophoneCaptureService only supports microphone capture.");
        }

        if (_audioQueue != IntPtr.Zero)
        {
            return Task.CompletedTask;
        }

        _state = AudioCaptureState.Starting;
        _selfHandle = GCHandle.Alloc(this);
        _callbackDelegate = OnAudioQueueInput;

        var format = new AudioStreamBasicDescription
        {
            mSampleRate = SampleRate,
            mFormatID = kAudioFormatLinearPCM,
            mFormatFlags = kLinearPCMFormatFlagIsSignedInteger | kLinearPCMFormatFlagIsPacked,
            mBytesPerPacket = (uint)(Channels * BitsPerSample / 8),
            mFramesPerPacket = 1,
            mBytesPerFrame = (uint)(Channels * BitsPerSample / 8),
            mChannelsPerFrame = (uint)Channels,
            mBitsPerChannel = (uint)BitsPerSample,
            mReserved = 0,
        };

        var status = AudioQueueNewInput(
            ref format,
            _callbackDelegate,
            GCHandle.ToIntPtr(_selfHandle),
            IntPtr.Zero, // run on audio thread
            IntPtr.Zero,
            0,
            out _audioQueue);

        if (status != 0)
        {
            _state = AudioCaptureState.Faulted;
            throw new InvalidOperationException($"AudioQueueNewInput failed: {status}");
        }

        for (var i = 0; i < BufferCount; i++)
        {
            AudioQueueAllocateBuffer(_audioQueue, BufferSize, out _buffers[i]);
            AudioQueueEnqueueBuffer(_audioQueue, _buffers[i], 0, IntPtr.Zero);
        }

        AudioQueueStart(_audioQueue, IntPtr.Zero);
        _state = AudioCaptureState.Capturing;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_audioQueue == IntPtr.Zero)
        {
            _state = AudioCaptureState.Idle;
            return Task.CompletedTask;
        }

        _state = AudioCaptureState.Stopping;
        AudioQueueStop(_audioQueue, true);
        AudioQueueDispose(_audioQueue, true);
        _audioQueue = IntPtr.Zero;

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }

        _state = AudioCaptureState.Idle;
        return Task.CompletedTask;
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    // ── AudioQueue callback (called on CoreAudio thread) ──────────────────────

    private void OnAudioQueueInput(
        IntPtr inUserData,
        IntPtr inAQ,
        IntPtr inBuffer,
        ref AudioTimeStamp inStartTime,
        uint inNumberPacketDescriptions,
        IntPtr inPacketDescs)
    {
        if (_audioQueue == IntPtr.Zero)
        {
            return;
        }

        // AudioQueueBuffer layout:
        //  offset 0: uint32 mAudioDataBytesCapacity
        //  offset 4: void*  mAudioData
        //  offset 4+ptr: uint32 mAudioDataByteSize
        var bytesCapacity = Marshal.ReadInt32(inBuffer, 0);
        var audioDataPtr = Marshal.ReadIntPtr(inBuffer, 4);
        var bytesRecorded = Marshal.ReadInt32(inBuffer, 4 + IntPtr.Size);

        if (bytesRecorded > 0 && audioDataPtr != IntPtr.Zero)
        {
            var data = new byte[bytesRecorded];
            Marshal.Copy(audioDataPtr, data, 0, bytesRecorded);
            var duration = TimeSpan.FromSeconds((double)bytesRecorded / (SampleRate * Channels * (BitsPerSample / 8)));

            AudioChunkCaptured?.Invoke(
                this,
                new AudioChunk(data, SampleRate, Channels, BitsPerSample, duration, DateTimeOffset.UtcNow)
            );
        }

        // Re-enqueue the buffer for next fill
        if (_audioQueue != IntPtr.Zero)
        {
            AudioQueueEnqueueBuffer(_audioQueue, inBuffer, 0, IntPtr.Zero);
        }
    }
}
