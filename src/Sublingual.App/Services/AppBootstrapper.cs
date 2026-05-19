using Microsoft.Extensions.DependencyInjection;
using Sublingual.App.ViewModels;
using Sublingual.App.Views;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;
using Sublingual.Infrastructure.Audio.Processing;
using Sublingual.Infrastructure.Audio.Windows;
using Sublingual.Infrastructure.Audio.macOS;

namespace Sublingual.App.Services;

public sealed class AppBootstrapper : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public AppBootstrapper()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public MainWindow CreateMainWindow()
    {
        return new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
        };
    }

    public OverlayWindow CreateOverlayWindow()
    {
        return new OverlayWindow
        {
            DataContext = _serviceProvider.GetRequiredService<OverlayWindowViewModel>(),
        };
    }

    public void Dispose()
    {
        if (_serviceProvider.GetService<MainWindowViewModel>() is IDisposable mainWindowViewModel)
        {
            mainWindowViewModel.Dispose();
        }

        if (_serviceProvider.GetService<OverlayWindowViewModel>() is IDisposable overlayWindowViewModel)
        {
            overlayWindowViewModel.Dispose();
        }

        _serviceProvider.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IAudioCaptureService>(_ => CreateAudioCaptureService());
        services.AddSingleton<IAudioChunkProcessor, FixedWindowAudioChunkProcessor>();
        services.AddSingleton<ITranscriptionService, MockTranscriptionService>();
        services.AddSingleton<ITranslationService, MockTranslationService>();

        services.AddSingleton<Sublingual.Application.Audio.StartCaptureUseCase>();
        services.AddSingleton<Sublingual.Application.Audio.StopCaptureUseCase>();
        services.AddSingleton<Sublingual.Application.Audio.ProcessAudioChunkUseCase>();
        services.AddSingleton<Sublingual.Application.Audio.TranscribeAudioChunkUseCase>();
        services.AddSingleton<Sublingual.Application.Audio.TranslateTranscriptUseCase>();

        services.AddSingleton<AudioCaptureDebugSession>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<OverlayWindowViewModel>();
    }

    private static IAudioCaptureService CreateAudioCaptureService()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WasapiLoopbackCaptureService();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ScreenCaptureKitCaptureService();
        }

        return DesignTimeAudioCaptureService.Instance;
    }
}
