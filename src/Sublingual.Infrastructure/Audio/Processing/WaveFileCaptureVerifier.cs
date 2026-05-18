using NAudio.Wave;
using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class WaveFileCaptureVerifier : IDisposable
{
    private readonly string _outputPath;
    private WaveFileWriter? _writer;

    public WaveFileCaptureVerifier(string outputPath)
    {
        _outputPath = outputPath;
    }

    public void Append(AudioChunk chunk)
    {
        _writer ??= CreateWriter(chunk);
        _writer.Write(chunk.Data, 0, chunk.Data.Length);
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
    }

    private WaveFileWriter CreateWriter(AudioChunk chunk)
    {
        var directory = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WaveFormat waveFormat = chunk.BitsPerSample == 32
            ? WaveFormat.CreateIeeeFloatWaveFormat(chunk.SampleRate, chunk.Channels)
            : new WaveFormat(chunk.SampleRate, chunk.BitsPerSample, chunk.Channels);

        return new WaveFileWriter(_outputPath, waveFormat);
    }
}
