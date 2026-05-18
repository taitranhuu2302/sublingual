namespace Sublingual.Domain.Audio;

public interface IAudioChunkProcessor
{
    IReadOnlyList<AudioChunk> Process(AudioChunk inputChunk);
}
