using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class VoskInputVerifier
{
    public void Verify(AudioChunk chunk)
    {
        if (chunk.Data.Length == 0)
        {
            throw new InvalidOperationException("Vosk input chunk is empty.");
        }

        if (chunk.SampleRate != AudioFormatNormalizer.TargetSampleRate
            || chunk.Channels != AudioFormatNormalizer.TargetChannels
            || chunk.BitsPerSample != AudioFormatNormalizer.TargetBitsPerSample)
        {
            throw new InvalidOperationException(
                $"Vosk input must be {AudioFormatNormalizer.TargetSampleRate}Hz mono PCM16, got {chunk.SampleRate}Hz, {chunk.Channels}ch, {chunk.BitsPerSample}bit."
            );
        }

        if (chunk.Duration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Vosk input chunk duration must be greater than zero.");
        }
    }

    public string Describe(AudioChunk chunk)
    {
        return $"Vosk input: {chunk.SampleRate}Hz | {chunk.Channels}ch | {chunk.BitsPerSample}bit | {chunk.Duration.TotalMilliseconds:F0}ms | {chunk.Data.Length}B";
    }
}
