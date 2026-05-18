using Sublingual.Domain.Transcription;

namespace Sublingual.Application.Audio;

public sealed class TranscribeAudioChunkUseCase(
    ITranscriptionService transcriptionService
)
{
    public Task<TranscriptionResult> ExecuteAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default
    ) => transcriptionService.TranscribeAsync(request, cancellationToken);
}
