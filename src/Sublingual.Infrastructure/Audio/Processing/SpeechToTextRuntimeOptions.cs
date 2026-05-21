namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class SpeechToTextRuntimeOptions
{
    public TimeSpan ChunkWindow { get; private set; } = TimeSpan.FromMilliseconds(350);

    public void ApplyChunkPreset(string? preset)
    {
        ChunkWindow = NormalizeChunkPreset(preset) switch
        {
            SpeechToTextChunkPresets.Fast => TimeSpan.FromMilliseconds(275),
            SpeechToTextChunkPresets.Accurate => TimeSpan.FromMilliseconds(500),
            _ => TimeSpan.FromMilliseconds(350),
        };
    }

    public static string NormalizeChunkPreset(string? preset)
    {
        return string.Equals(preset, SpeechToTextChunkPresets.Fast, StringComparison.OrdinalIgnoreCase)
            ? SpeechToTextChunkPresets.Fast
            : string.Equals(preset, SpeechToTextChunkPresets.Accurate, StringComparison.OrdinalIgnoreCase)
                ? SpeechToTextChunkPresets.Accurate
                : SpeechToTextChunkPresets.Balanced;
    }
}

public static class SpeechToTextChunkPresets
{
    public const string Fast = "Fast";
    public const string Balanced = "Balanced";
    public const string Accurate = "Accurate";
}
