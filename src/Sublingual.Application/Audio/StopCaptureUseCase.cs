using Sublingual.Domain.Audio;

namespace Sublingual.Application.Audio;

public sealed class StopCaptureUseCase(IAudioCaptureService audioCaptureService)
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        audioCaptureService.StopAsync(cancellationToken);
}
