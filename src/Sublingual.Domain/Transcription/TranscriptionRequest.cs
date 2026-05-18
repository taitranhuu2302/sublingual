using Sublingual.Domain.Audio;

namespace Sublingual.Domain.Transcription;

public sealed record TranscriptionRequest(
    AudioChunk Chunk,
    string Language
);
