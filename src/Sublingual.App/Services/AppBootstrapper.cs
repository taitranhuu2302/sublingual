using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sublingual.App.Models;
using Sublingual.App.Services.Translation;
using Sublingual.App.Services.Logging;
using Sublingual.App.ViewModels;
using Sublingual.App.Views;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;
using Sublingual.Infrastructure.Audio;
using Sublingual.Infrastructure.Audio.Processing;
using Sublingual.Infrastructure.Audio.Windows;
using Sublingual.Infrastructure.Audio.macOS;

namespace Sublingual.App.Services;

public sealed class AppBootstrapper : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<AppBootstrapper> _logger;

    public IServiceProvider Services => _serviceProvider;

    public AppBootstrapper()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILogger<AppBootstrapper>>();
        _logger.LogInformation("DI container built");
    }

    public MainWindow CreateMainWindow()
    {
        var vm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        return new MainWindow
        {
            DataContext = vm,
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
        _logger.LogInformation("Disposing bootstrapper");
        if (_serviceProvider.GetService<MainWindowViewModel>() is IDisposable mainWindowViewModel)
        {
            mainWindowViewModel.Dispose();
        }

        if (_serviceProvider.GetService<OverlayWindowViewModel>() is IDisposable overlayWindowViewModel)
        {
            overlayWindowViewModel.Dispose();
        }

        _serviceProvider.Dispose();

        if (AppLog.Factory is IDisposable disposableFactory)
        {
            disposableFactory.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var logDir = LoggingBootstrapper.ResolveLogDirectory();
        var loggerFactory = LoggingBootstrapper.CreateLoggerFactory(logDir);
        AppLog.Initialize(loggerFactory);
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        services.AddSingleton<AppSettingsStore>();
        services.AddSingleton<LocalSqliteDatabase>();
        services.AddSingleton<SessionIndexStore>();

        services.AddSingleton(provider =>
        {
            var runtimeOptions = new SpeechToTextRuntimeOptions();
            var settings = provider.GetRequiredService<AppSettingsStore>().Load();
            runtimeOptions.ApplyChunkPreset(settings.SpeechToText.RealtimeChunkPreset);
            return runtimeOptions;
        });
        services.AddSingleton<IAudioCaptureService>(provider =>
        {
            var capture = CreateAudioCaptureService();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AudioCapture");
            logger.LogInformation("Resolved capture service: {CaptureType}", capture.GetType().Name);
            return capture;
        });
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
        services.AddSingleton<ITranslationProvider, TranslateServiceLocalTranslationProvider>();
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

        // Speaking Practice feature removed.
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

    private static IAudioCaptureService CreateMicrophoneCaptureService()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WasapiMicrophoneCaptureService();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new CoreAudioMicrophoneCaptureService();
        }

        return DesignTimeAudioCaptureService.Instance;
    }
}
