using System.Runtime.InteropServices;
using Sublingual.Domain.Audio;
using Sublingual.Infrastructure.Audio.macOS;
using Sublingual.Infrastructure.Audio.Processing;
using Sublingual.Interop.macOS;

namespace Sublingual.Desktop;

public static class MacOsDebugCaptureRunner
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!OperatingSystem.IsMacOS())
            {
                Console.WriteLine("[Sublingual] Debug capture runner skipped: macOS only.");
                return;
            }

            var nativeLibraryPath = TryResolveNativeLibraryPath();
            if (nativeLibraryPath is null)
            {
                Console.WriteLine("[Sublingual] Could not locate libScreenCaptureKitBridge.dylib.");
                return;
            }

            ScreenCaptureKitNative.ConfigureLibraryPath(nativeLibraryPath);

            var outputPath = Path.Combine(Environment.CurrentDirectory, "system-audio.wav");
            await using var lifetime = new DebugCaptureLifetime();
            using var verifier = new WaveFileCaptureVerifier(outputPath);
            using var captureService = new ScreenCaptureKitCaptureService();
            var chunkCount = 0;
            long totalBytes = 0;

            captureService.AudioChunkCaptured += (_, chunk) =>
            {
                chunkCount += 1;
                totalBytes += chunk.Data.Length;
                if (chunkCount <= 5 || chunkCount % 50 == 0)
                {
                    Console.WriteLine(
                        $"[Sublingual] Chunk #{chunkCount}: bytes={chunk.Data.Length}, sampleRate={chunk.SampleRate}, channels={chunk.Channels}, bits={chunk.BitsPerSample}, durationMs={chunk.Duration.TotalMilliseconds:F2}"
                    );
                }

                verifier.Append(chunk);
            };

            Console.WriteLine($"[Sublingual] Starting debug system capture -> {outputPath}");
            await captureService.StartAsync(
                new AudioCaptureRequest(AudioSourceType.System, null, 48_000, 2),
                cancellationToken
            );

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

            await captureService.StopAsync(cancellationToken);
            Console.WriteLine($"[Sublingual] Chunk summary: count={chunkCount}, totalBytes={totalBytes}");
            Console.WriteLine($"[Sublingual] Debug capture completed -> {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sublingual] Debug capture failed: {ex.Message}");
        }
    }

    private static string? TryResolveNativeLibraryPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "native",
                "macos",
                "ScreenCaptureKitBridge",
                "build",
                "libScreenCaptureKitBridge.dylib"
            );

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private sealed class DebugCaptureLifetime : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
