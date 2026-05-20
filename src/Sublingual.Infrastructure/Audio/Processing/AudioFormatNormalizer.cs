using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class AudioFormatNormalizer
{
    public const int TargetSampleRate = 16_000;
    public const int TargetChannels = 1;
    public const int TargetBitsPerSample = 16;

    public AudioChunk NormalizeForSpeechRecognition(AudioChunk chunk)
    {
        if (chunk.Data.Length == 0)
        {
            return new AudioChunk([], TargetSampleRate, TargetChannels, TargetBitsPerSample, TimeSpan.Zero, chunk.CapturedAt);
        }

        var monoFloat = ConvertToMonoFloatSamples(chunk);
        if (monoFloat.Length == 0)
        {
            return new AudioChunk([], TargetSampleRate, TargetChannels, TargetBitsPerSample, TimeSpan.Zero, chunk.CapturedAt);
        }

        var resampled = chunk.SampleRate == TargetSampleRate
            ? monoFloat
            : ResampleLinear(monoFloat, chunk.SampleRate, TargetSampleRate);

        var pcm16 = ConvertFloatToPcm16(resampled);
        var duration = TimeSpan.FromSeconds(resampled.Length / (double)TargetSampleRate);

        return new AudioChunk(
            pcm16,
            TargetSampleRate,
            TargetChannels,
            TargetBitsPerSample,
            duration,
            chunk.CapturedAt
        );
    }

    private static float[] ConvertToMonoFloatSamples(AudioChunk chunk)
    {
        if (chunk.Channels <= 0)
        {
            return [];
        }

        if (chunk.BitsPerSample == 32)
        {
            var frameCount = chunk.Data.Length / sizeof(float) / chunk.Channels;
            var mono = new float[frameCount];
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                double sum = 0;
                for (var channelIndex = 0; channelIndex < chunk.Channels; channelIndex++)
                {
                    var sampleOffset = (frameIndex * chunk.Channels + channelIndex) * sizeof(float);
                    sum += BitConverter.ToSingle(chunk.Data, sampleOffset);
                }

                mono[frameIndex] = (float)(sum / chunk.Channels);
            }

            return mono;
        }

        if (chunk.BitsPerSample == 16)
        {
            var frameCount = chunk.Data.Length / sizeof(short) / chunk.Channels;
            var mono = new float[frameCount];
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                int sum = 0;
                for (var channelIndex = 0; channelIndex < chunk.Channels; channelIndex++)
                {
                    var sampleOffset = (frameIndex * chunk.Channels + channelIndex) * sizeof(short);
                    sum += BitConverter.ToInt16(chunk.Data, sampleOffset);
                }

                mono[frameIndex] = (sum / (float)chunk.Channels) / short.MaxValue;
            }

            return mono;
        }

        throw new NotSupportedException(
            $"Unsupported audio format: {chunk.SampleRate}Hz, {chunk.Channels}ch, {chunk.BitsPerSample}bit."
        );
    }

    private static float[] ResampleLinear(float[] input, int sourceSampleRate, int targetSampleRate)
    {
        if (input.Length == 0)
        {
            return [];
        }

        if (sourceSampleRate <= 0 || targetSampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSampleRate), "Sample rates must be positive.");
        }

        if (sourceSampleRate == targetSampleRate)
        {
            return input.ToArray();
        }

        var outputLength = Math.Max(1, (int)Math.Round(input.Length * (targetSampleRate / (double)sourceSampleRate)));
        var output = new float[outputLength];

        for (var index = 0; index < outputLength; index++)
        {
            var sourcePosition = index * (sourceSampleRate / (double)targetSampleRate);
            var leftIndex = Math.Clamp((int)Math.Floor(sourcePosition), 0, input.Length - 1);
            var rightIndex = Math.Clamp(leftIndex + 1, 0, input.Length - 1);
            var fraction = sourcePosition - leftIndex;
            output[index] = (float)((input[leftIndex] * (1 - fraction)) + (input[rightIndex] * fraction));
        }

        return output;
    }

    private static byte[] ConvertFloatToPcm16(float[] samples)
    {
        var output = new byte[samples.Length * sizeof(short)];
        for (var index = 0; index < samples.Length; index++)
        {
            var pcm16 = (short)Math.Clamp(samples[index] * short.MaxValue, short.MinValue, short.MaxValue);
            var outputOffset = index * sizeof(short);
            output[outputOffset] = (byte)(pcm16 & 0xFF);
            output[outputOffset + 1] = (byte)((pcm16 >> 8) & 0xFF);
        }

        return output;
    }
}
