using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class FixedWindowAudioChunkProcessor : IAudioChunkProcessor
{
    private readonly TimeSpan _targetWindow;
    private readonly List<byte> _buffer = [];

    public FixedWindowAudioChunkProcessor()
        : this(TimeSpan.FromMilliseconds(750))
    {
    }

    public FixedWindowAudioChunkProcessor(TimeSpan targetWindow)
    {
        _targetWindow = targetWindow;
    }

    public IReadOnlyList<AudioChunk> Process(AudioChunk inputChunk)
    {
        if (inputChunk.Data.Length == 0)
        {
            return [];
        }

        _buffer.AddRange(inputChunk.Data);

        var bytesPerSample = Math.Max(1, inputChunk.BitsPerSample / 8);
        var bytesPerSecond = Math.Max(1, inputChunk.SampleRate * inputChunk.Channels * bytesPerSample);
        var targetByteCount = Math.Max(bytesPerSample, (int)Math.Round(bytesPerSecond * _targetWindow.TotalSeconds));
        targetByteCount -= targetByteCount % bytesPerSample;

        if (targetByteCount <= 0)
        {
            return [];
        }

        var chunks = new List<AudioChunk>();
        while (_buffer.Count >= targetByteCount)
        {
            var data = _buffer.GetRange(0, targetByteCount).ToArray();
            _buffer.RemoveRange(0, targetByteCount);

            chunks.Add(new AudioChunk(
                data,
                inputChunk.SampleRate,
                inputChunk.Channels,
                inputChunk.BitsPerSample,
                TimeSpan.FromSeconds((double)targetByteCount / bytesPerSecond),
                inputChunk.CapturedAt));
        }

        return chunks;
    }
}
