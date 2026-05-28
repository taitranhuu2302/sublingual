using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class FixedWindowAudioChunkProcessor : IAudioChunkProcessor
{
    private readonly SpeechToTextRuntimeOptions _runtimeOptions;
    private readonly List<byte> _buffer = [];
    private readonly float _vadThreshold;

    public FixedWindowAudioChunkProcessor(SpeechToTextRuntimeOptions runtimeOptions, float vadThreshold = 0.01f)
    {
        _runtimeOptions = runtimeOptions;
        _vadThreshold = vadThreshold;
    }

    public IReadOnlyList<AudioChunk> Process(AudioChunk inputChunk)
    {
        if (inputChunk.Data.Length == 0)
        {
            return [];
        }

        if (!HasVoiceActivity(inputChunk))
        {
            if (_buffer.Count == 0)
            {
                return [];
            }

            var flushed = FlushBuffer(inputChunk);
            return flushed.Count > 0 ? flushed : [];
        }

        _buffer.AddRange(inputChunk.Data);

        return FlushBuffer(inputChunk);
    }

    private IReadOnlyList<AudioChunk> FlushBuffer(AudioChunk template)
    {
        if (_buffer.Count == 0)
        {
            return [];
        }

        var targetWindow = _runtimeOptions.ChunkWindow;
        var bytesPerSample = Math.Max(1, template.BitsPerSample / 8);
        var bytesPerSecond = Math.Max(1, template.SampleRate * template.Channels * bytesPerSample);
        var targetByteCount = Math.Max(bytesPerSample, (int)Math.Round(bytesPerSecond * targetWindow.TotalSeconds));
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
                template.SampleRate,
                template.Channels,
                template.BitsPerSample,
                TimeSpan.FromSeconds((double)targetByteCount / bytesPerSecond),
                template.CapturedAt));
        }

        return chunks;
    }

    private bool HasVoiceActivity(AudioChunk chunk)
    {
        var bytesPerSample = Math.Max(1, chunk.BitsPerSample / 8);
        var sampleCount = chunk.Data.Length / bytesPerSample;

        if (sampleCount < 8)
        {
            return false;
        }

        double sumSquares = 0;
        if (chunk.BitsPerSample == 16)
        {
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = BitConverter.ToInt16(chunk.Data, i * bytesPerSample);
                sumSquares += sample * sample;
            }
        }
        else if (chunk.BitsPerSample == 32)
        {
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = BitConverter.ToSingle(chunk.Data, i * bytesPerSample);
                sumSquares += sample * sample;
            }
        }
        else
        {
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = (sbyte)chunk.Data[i * bytesPerSample];
                sumSquares += sample * sample;
            }
        }

        var rms = Math.Sqrt(sumSquares / sampleCount);
        var maxValue = chunk.BitsPerSample switch
        {
            16 => short.MaxValue,
            32 => 1.0,
            _ => sbyte.MaxValue,
        };

        return rms / maxValue >= _vadThreshold;
    }
}
