using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class AdaptiveVad
{
    private const double NoiseFloorDecay = 0.999;
    private const double NoiseFloorMin = 0.0001;
    private const double SpeechThresholdMultiplier = 2.5;
    private const double ZcrSpeechThreshold = 0.3;
    private const double MinRmsForZcr = 0.002;
    private const int MinSpeechFrames = 3;
    private const int HangoverFrames = 12;

    private double _noiseFloor = NoiseFloorMin;
    private int _consecutiveSpeech;
    private int _hangoverCounter;
    private bool _isActive;

    public bool IsActive => _isActive;

    public bool ProcessFrame(AudioChunk chunk)
    {
        if (chunk.Data.Length < 64)
        {
            return false;
        }

        var sampleCount = chunk.Data.Length / sizeof(short);
        long sumSquares = 0;
        int zeroCrossings = 0;
        bool prevPositive = true;

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(chunk.Data, i * sizeof(short));
            sumSquares += sample * sample;

            var isPositive = sample >= 0;
            if (i > 0 && isPositive != prevPositive)
            {
                zeroCrossings++;
            }
            prevPositive = isPositive;
        }

        var rms = Math.Sqrt(sumSquares / (double)sampleCount);
        var normalizedRms = rms / short.MaxValue;
        var zcr = (double)zeroCrossings / sampleCount;

        _noiseFloor = Math.Max(
            NoiseFloorMin,
            _noiseFloor * NoiseFloorDecay + normalizedRms * (1 - NoiseFloorDecay));

        var threshold = _noiseFloor * SpeechThresholdMultiplier;
        var isSpeech = normalizedRms >= threshold
            || (normalizedRms >= MinRmsForZcr && zcr >= ZcrSpeechThreshold);

        if (isSpeech)
        {
            _consecutiveSpeech++;
            _hangoverCounter = HangoverFrames;
        }
        else
        {
            _consecutiveSpeech = 0;

            if (_hangoverCounter > 0)
            {
                _hangoverCounter--;
                isSpeech = true;
            }
        }

        if (_consecutiveSpeech >= MinSpeechFrames)
        {
            _isActive = true;
        }
        else if (_hangoverCounter == 0)
        {
            _isActive = false;
        }

        return _isActive;
    }

    public void Reset()
    {
        _noiseFloor = NoiseFloorMin;
        _consecutiveSpeech = 0;
        _hangoverCounter = 0;
        _isActive = false;
    }
}
