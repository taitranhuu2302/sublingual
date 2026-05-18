using Sublingual.Domain.Audio;

namespace Sublingual.Application.Audio;

public sealed class StartCaptureUseCase(IAudioCaptureService audioCaptureService)
{
    public Task ExecuteAsync(
        AudioCaptureRequest request,
        CancellationToken cancellationToken = default
    ) => audioCaptureService.StartAsync(request, cancellationToken);
}
