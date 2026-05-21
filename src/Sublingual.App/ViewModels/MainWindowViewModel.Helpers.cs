using System.Diagnostics;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.App.Services.Translation;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static string DetectPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unsupported";
    }

    private static double CalculateAudioLevelPercent(AudioChunk chunk)
    {
        if (chunk.BitsPerSample == 32 && chunk.Data.Length >= 4)
        {
            var n = chunk.Data.Length / 4;
            double sum = 0;
            for (var i = 0; i < chunk.Data.Length; i += 4)
            {
                var s = (double)BitConverter.ToSingle(chunk.Data, i);
                sum += s * s;
            }
            return Math.Clamp(Math.Sqrt(sum / n) * 100, 0, 100);
        }

        if (chunk.BitsPerSample == 16 && chunk.Data.Length >= 2)
        {
            var n = chunk.Data.Length / 2;
            double sum = 0;
            for (var i = 0; i < chunk.Data.Length; i += 2)
            {
                var s = BitConverter.ToInt16(chunk.Data, i) / 32768d;
                sum += s * s;
            }
            return Math.Clamp(Math.Sqrt(sum / n) * 180, 0, 100);
        }

        return 0;
    }

    private void PushWaveformSample(double level)
    {
        _waveformSamples.Enqueue(level);
        while (_waveformSamples.Count > WaveformSampleCapacity)
        {
            _waveformSamples.Dequeue();
        }

        UpdateAudioLevelBars(_waveformSamples.ToArray());
    }

    private void UpdateAudioLevelBars(IReadOnlyList<double> samples)
    {
        for (var i = 0; i < AudioLevelBars.Count; i++)
        {
            var normalized = i < samples.Count ? Math.Clamp(samples[i] / 100d, 0, 1) : 0;
            var eased = Math.Pow(normalized, 0.52);
            AudioLevelBars[i].Height = 8 + (eased * 58);
            AudioLevelBars[i].Opacity = 0.30 + (eased * 0.70);
        }
    }

    private void AppendRuntimeLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var existingLines = string.IsNullOrWhiteSpace(RuntimeLog)
            ? []
            : RuntimeLog.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var messageLines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var combined = existingLines.Concat(messageLines).ToList();
        if (combined.Count > RuntimeLogLineLimit)
        {
            combined = combined.Skip(combined.Count - RuntimeLogLineLimit).ToList();
        }

        RuntimeLog = string.Join(Environment.NewLine, combined);
    }

    private static AudioCaptureDebugSession CreateDesignTimeSession()
    {
        var captureService = DesignTimeAudioCaptureService.Instance;
        var settingsStore = new AppSettingsStore();
        return new AudioCaptureDebugSession(
            captureService,
            new Sublingual.Application.Audio.StartCaptureUseCase(captureService),
            new Sublingual.Application.Audio.StopCaptureUseCase(captureService),
            new Sublingual.Application.Audio.ProcessAudioChunkUseCase(
                new Sublingual.Infrastructure.Audio.Processing.PassthroughAudioChunkProcessor()),
            new Sublingual.Application.Audio.TranscribeAudioChunkUseCase(new MockTranscriptionService()),
            new ConfigurableTranslationService(
                [
                    new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                    new LibreTranslateTranslationProvider(new HttpClient()),
                ],
                settingsStore
            ),
            new CaptureSessionStorage(settingsStore),
            new Sublingual.Infrastructure.Audio.Processing.AudioFormatNormalizer(),
            new Sublingual.Infrastructure.Audio.Processing.VoskInputVerifier(),
            settingsStore);
    }

    private static SpeechToTextModelCatalog CreateDesignTimeModelCatalog()
    {
        return new SpeechToTextModelCatalog(new AppSettingsStore());
    }
}
