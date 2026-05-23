using System.Diagnostics;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.Infrastructure.TTS;

/// <summary>
/// Local OS text-to-speech engine.
/// Windows: System.Speech.Synthesis via <c>ProcessStartInfo</c> (PowerShell).
/// macOS: <c>/usr/bin/say</c> subprocess.
/// </summary>
public sealed class LocalSystemTtsService : ITtsService, IDisposable
{
    private volatile bool _isSpeaking;
    private CancellationTokenSource? _activeCts;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsSpeaking => _isSpeaking;

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Cancel any previous speech.
        StopSpeaking();

        await _gate.WaitAsync(cancellationToken);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCts = cts;

        try
        {
            _isSpeaking = true;
            await RunSpeechProcessAsync(text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user skips or restarts.
        }
        finally
        {
            _isSpeaking = false;
            _activeCts = null;
            cts.Dispose();
            _gate.Release();
        }
    }

    public void StopSpeaking()
    {
        _activeCts?.Cancel();
    }

    public void Dispose()
    {
        StopSpeaking();
        _activeCts?.Dispose();
        _gate.Dispose();
    }

    private static Task RunSpeechProcessAsync(string text, CancellationToken cancellationToken)
    {
        var sanitized = SanitizeForShell(text);

        ProcessStartInfo psi;
        if (OperatingSystem.IsMacOS())
        {
            psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/say",
                Arguments = sanitized,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
        }
        else if (OperatingSystem.IsWindows())
        {
            // Use PowerShell to drive the built-in speech synthesizer on Windows.
            var script = $"Add-Type -AssemblyName System.Speech; " +
                         $"$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                         $"$s.Speak('{sanitized}')";
            psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
        }
        else
        {
            return Task.CompletedTask;
        }

        return RunProcessAsync(psi, cancellationToken);
    }

    private static async Task RunProcessAsync(ProcessStartInfo psi, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        process.Start();

        await using var reg = cancellationToken.Register(() =>
        {
            try { process.Kill(); } catch { /* already exited */ }
            tcs.TrySetCanceled(cancellationToken);
        });

        await tcs.Task;
    }

    private static string SanitizeForShell(string text)
    {
        // Remove characters that could break shell arguments.
        return text
            .Replace("\"", " ")
            .Replace("'", " ")
            .Replace("`", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }
}
