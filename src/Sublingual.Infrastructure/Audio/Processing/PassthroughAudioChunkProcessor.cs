using Sublingual.Domain.Audio;

namespace Sublingual.Infrastructure.Audio.Processing;

public sealed class PassthroughAudioChunkProcessor : IAudioChunkProcessor
{
    public IReadOnlyList<AudioChunk> Process(AudioChunk inputChunk) => [inputChunk];
}
