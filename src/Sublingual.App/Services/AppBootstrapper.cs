using Microsoft.Extensions.DependencyInjection;
using Sublingual.App.Services.Translation;
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

    public IServiceProvider Services => _serviceProvider;

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
        services.AddSingleton<AppSettingsStore>();
        services.AddSingleton(provider =>
        {
            var runtimeOptions = new SpeechToTextRuntimeOptions();
            var settings = provider.GetRequiredService<AppSettingsStore>().Load();
            runtimeOptions.ApplyChunkPreset(settings.SpeechToText.RealtimeChunkPreset);
            return runtimeOptions;
        });
        services.AddSingleton<IAudioCaptureService>(_ => CreateAudioCaptureService());
        services.AddSingleton<IAudioChunkProcessor, FixedWindowAudioChunkProcessor>();
        services.AddSingleton<AudioFormatNormalizer>();
        services.AddSingleton<VoskInputVerifier>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<SpeechToTextModelCatalog>();
        services.AddSingleton<SpeechToTextModelSourceCatalog>();
        services.AddSingleton<SpeechToTextModelImporter>();
        services.AddSingleton<SpeechToTextDefaultModelInstaller>();
        services.AddSingleton<CaptureSessionStorage>();
        services.AddSingleton<RealtimeTranslationScheduler>();
        services.AddSingleton<VoskTranscriptionService>();
        services.AddSingleton<ITranscriptionService>(provider => provider.GetRequiredService<VoskTranscriptionService>());
        services.AddSingleton<ITranslationProvider, GoogleTranslateFreeApiTranslationProvider>();
        services.AddSingleton<ITranslationProvider, LibreTranslateTranslationProvider>();
        services.AddSingleton<ITranslationExecutionService, ConfigurableTranslationService>();
        services.AddSingleton<ITranslationService>(provider => provider.GetRequiredService<ITranslationExecutionService>());

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
