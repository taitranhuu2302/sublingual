using Sublingual.Domain.Audio;

namespace Sublingual.Application.Audio;

public sealed class ProcessAudioChunkUseCase(IAudioChunkProcessor audioChunkProcessor)
{
    public IReadOnlyList<AudioChunk> Execute(AudioChunk inputChunk) =>
        audioChunkProcessor.Process(inputChunk);
}
