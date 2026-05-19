using System.ComponentModel;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Services;
using Sublingual.Domain.Audio;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private readonly string _outputPath;
    private long _totalBytesCaptured;
    private int _chunkCount;
    private bool _disposed;

    // Wired by App.axaml.cs to show/hide the overlay window
    public Action? ToggleOverlayAction { get; set; }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isCapturing;
    [ObservableProperty] private bool isOverlayVisible;
    [ObservableProperty] private string selectedDeviceId = string.Empty;
    [ObservableProperty] private AudioDeviceItemViewModel? selectedDevice;
    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private string runtimeLog;
    [ObservableProperty] private string outputFilePath;
    [ObservableProperty] private int chunkCount;
    [ObservableProperty] private string totalBytesText;
    [ObservableProperty] private string currentPlatform;
    [ObservableProperty] private string captureState;
    [ObservableProperty] private string pipelineSummary;
    [ObservableProperty] private string selectedDeviceName;
    [ObservableProperty] private double audioLevel;
    [ObservableProperty] private double peakAudioLevel;
    [ObservableProperty] private string audioLevelText;
    [ObservableProperty] private string partialTranscript;
    [ObservableProperty] private string finalTranscript;
    [ObservableProperty] private string transcriptStatus;
    [ObservableProperty] private string partialTranslatedTranscript;
    [ObservableProperty] private string finalTranslatedTranscript;

    // Overlay settings — bound in the Overlay tab
    [ObservableProperty] private double overlayFontSize = 26;
    [ObservableProperty] private double overlayWidth = 720;
    [ObservableProperty] private double overlayHeight = 200;
    [ObservableProperty] private string overlayTheme = "Dark";
    [ObservableProperty] private double overlayOpacity = 0.88;

    // Sidebar navigation
    [ObservableProperty] private string activeTab = "capture";

    // Computed visibility helpers
    public bool HasRuntimeLog => !string.IsNullOrWhiteSpace(RuntimeLog);
    public bool HasTranscript => !string.IsNullOrWhiteSpace(PartialTranscript) || !string.IsNullOrWhiteSpace(FinalTranscript);
    public bool HasPartialTranslation => !string.IsNullOrWhiteSpace(PartialTranslatedTranscript);
    public bool HasFinalTranslation => !string.IsNullOrWhiteSpace(FinalTranslatedTranscript);
    public string OverlayToggleLabel => IsOverlayVisible ? "Hide Overlay" : "Show Overlay";
    public bool IsCaptureTabActive => string.Equals(ActiveTab, "capture", StringComparison.OrdinalIgnoreCase);
    public bool IsOverlayTabActive => string.Equals(ActiveTab, "overlay", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void SelectTab(string tab)
    {
        ActiveTab = tab;
    }

    public MainWindowViewModel(AudioCaptureDebugSession session)
    {
        _session = session;
        _session.ChunkObserved += OnChunkObserved;
        _outputPath = Path.Combine(Environment.CurrentDirectory, "system-audio.wav");

        Devices = [];
        statusMessage = "Ready.";
        runtimeLog = string.Empty;
        outputFilePath = _outputPath;
        totalBytesText = "0 bytes";
        currentPlatform = DetectPlatform();
        captureState = _session.State.ToString();
        pipelineSummary = "16kHz mono PCM16, 750ms fixed chunks";
        selectedDeviceName = "No device selected.";
        audioLevelText = "Silence";
        partialTranscript = string.Empty;
        finalTranscript = string.Empty;
        partialTranslatedTranscript = string.Empty;
        finalTranslatedTranscript = string.Empty;
        transcriptStatus = string.Empty;

        _session.TranscriptPreviewUpdated += OnTranscriptPreviewUpdated;
        PropertyChanged += OnPropertyChanged;
        _ = LoadDevicesAsync();
    }

    // Design-time constructor
    public MainWindowViewModel()
        : this(CreateDesignTimeSession())
    {
    }

    public ObservableCollection<AudioDeviceItemViewModel> Devices { get; }

    public bool HasDevices => Devices.Count > 0;
    public bool CanStartCapture => !IsBusy && !IsCapturing && HasDevices;
    public bool CanStopCapture => !IsBusy && IsCapturing;

    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    private async Task StartCaptureAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            _chunkCount = 0;
            _totalBytesCaptured = 0;
            ChunkCount = 0;
            TotalBytesText = "0 bytes";
            AudioLevel = 0;
            PeakAudioLevel = 0;
            AudioLevelText = "Listening...";
            PartialTranscript = string.Empty;
            FinalTranscript = string.Empty;
            PartialTranslatedTranscript = string.Empty;
            FinalTranslatedTranscript = string.Empty;
            TranscriptStatus = string.Empty;
            RuntimeLog = "Starting capture...";

            await _session.StartAsync(
                string.IsNullOrWhiteSpace(SelectedDevice?.Id) ? null : SelectedDevice.Id,
                _outputPath);

            IsCapturing = true;
            CaptureState = _session.State.ToString();
            StatusMessage = $"Capturing on {CurrentPlatform}.";
            RuntimeLog = $"Capture started. Writing to {_outputPath}";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStopCapture))]
    private async Task StopCaptureAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            await _session.StopAsync();
            IsCapturing = false;
            CaptureState = _session.State.ToString();
            AudioLevel = 0;
            AudioLevelText = "Silence";
            StatusMessage = "Capture stopped.";
            RuntimeLog = $"Stopped. {ChunkCount} chunks saved to {_outputPath}";
        });
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        await RunBusyOperationAsync(LoadDevicesAsync);
    }

    [RelayCommand]
    private void ToggleOverlay()
    {
        ToggleOverlayAction?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        PropertyChanged -= OnPropertyChanged;
        _session.ChunkObserved -= OnChunkObserved;
        _session.TranscriptPreviewUpdated -= OnTranscriptPreviewUpdated;
        _session.Dispose();
        _disposed = true;
    }

    private async Task LoadDevicesAsync()
    {
        var devices = await _session.GetAvailableDevicesAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Devices.Clear();
            foreach (var device in devices)
                Devices.Add(new AudioDeviceItemViewModel(device));

            var selected = Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault();
            SelectedDevice = selected;
            SelectedDeviceId = selected?.Id ?? string.Empty;
            SelectedDeviceName = selected?.Name ?? "No device selected.";
            CaptureState = _session.State.ToString();
            StatusMessage = devices.Count == 0
                ? $"No audio devices found for {CurrentPlatform}."
                : $"Found {devices.Count} device(s).";
            RuntimeLog = devices.Count == 0
                ? "No devices enumerated."
                : $"Default: {selected?.Name}";
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasDevices)));
            StartCaptureCommand.NotifyCanExecuteChanged();
        });
    }

    private async Task RunBusyOperationAsync(Func<Task> action)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StartCaptureCommand.NotifyCanExecuteChanged();
            StopCaptureCommand.NotifyCanExecuteChanged();
            await action();
        }
        catch (Exception ex)
        {
            CaptureState = AudioCaptureState.Faulted.ToString();
            StatusMessage = "Operation failed.";
            RuntimeLog = ex.Message;
            IsCapturing = false;
        }
        finally
        {
            IsBusy = false;
            CaptureState = _session.State.ToString();
            StartCaptureCommand.NotifyCanExecuteChanged();
            StopCaptureCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnChunkObserved(object? sender, AudioChunk chunk)
    {
        _chunkCount += 1;
        _totalBytesCaptured += chunk.Data.Length;

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ChunkCount = _chunkCount;
            TotalBytesText = $"{_totalBytesCaptured:N0} bytes";
            CaptureState = _session.State.ToString();
            AudioLevel = CalculateAudioLevelPercent(chunk);
            PeakAudioLevel = Math.Max(PeakAudioLevel, AudioLevel);
            AudioLevelText = $"Level {AudioLevel:0}%  Peak {PeakAudioLevel:0}%";
            RuntimeLog = $"Chunk #{_chunkCount}: {chunk.Data.Length} B | {chunk.SampleRate} Hz | {chunk.Channels}ch | {chunk.BitsPerSample}bit | {chunk.Duration.TotalMilliseconds:F0}ms";
        });
    }

    private void OnTranscriptPreviewUpdated(object? sender, TranscriptPreviewUpdate update)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            PartialTranscript = update.PartialText ?? string.Empty;
            PartialTranslatedTranscript = update.PartialTranslatedText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(update.FinalText))
                FinalTranscript = update.FinalText;
            if (!string.IsNullOrWhiteSpace(update.FinalTranslatedText))
                FinalTranslatedTranscript = update.FinalTranslatedText;
            if (!string.IsNullOrWhiteSpace(update.FinalText) || !string.IsNullOrWhiteSpace(update.PartialText))
                TranscriptStatus = $"Updated {update.UpdatedAt:HH:mm:ss}";
        });
    }

    private static string DetectPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unsupported";
    }

    private static double CalculateAudioLevelPercent(AudioChunk chunk)
    {
        // IEEE Float 32-bit — WASAPI loopback default
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

        // PCM 16-bit
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

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IsBusy) or nameof(IsCapturing))
        {
            StartCaptureCommand.NotifyCanExecuteChanged();
            StopCaptureCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(SelectedDevice) && SelectedDevice is not null)
        {
            SelectedDeviceId = SelectedDevice.Id;
            SelectedDeviceName = SelectedDevice.Name;
            StartCaptureCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(SelectedDeviceId))
            StartCaptureCommand.NotifyCanExecuteChanged();

        if (e.PropertyName is nameof(RuntimeLog))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasRuntimeLog)));

        if (e.PropertyName is nameof(PartialTranscript) or nameof(FinalTranscript))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranscript)));

        if (e.PropertyName == nameof(PartialTranslatedTranscript))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasPartialTranslation)));

        if (e.PropertyName == nameof(FinalTranslatedTranscript))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasFinalTranslation)));

        if (e.PropertyName == nameof(IsOverlayVisible))
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(OverlayToggleLabel)));

        if (e.PropertyName == nameof(ActiveTab))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCaptureTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsOverlayTabActive)));
        }
    }

    private static AudioCaptureDebugSession CreateDesignTimeSession()
    {
        var captureService = DesignTimeAudioCaptureService.Instance;
        return new AudioCaptureDebugSession(
            captureService,
            new Sublingual.Application.Audio.StartCaptureUseCase(captureService),
            new Sublingual.Application.Audio.StopCaptureUseCase(captureService),
            new Sublingual.Application.Audio.ProcessAudioChunkUseCase(
                new Sublingual.Infrastructure.Audio.Processing.PassthroughAudioChunkProcessor()),
            new Sublingual.Application.Audio.TranscribeAudioChunkUseCase(new MockTranscriptionService()),
            new Sublingual.Application.Audio.TranslateTranscriptUseCase(new MockTranslationService()));
    }
}

public sealed class AudioDeviceItemViewModel(AudioDevice device)
{
    public string Id { get; } = device.Id;
    public string Name { get; } = device.Name;
    public bool IsDefault { get; } = device.IsDefault;
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;
}
