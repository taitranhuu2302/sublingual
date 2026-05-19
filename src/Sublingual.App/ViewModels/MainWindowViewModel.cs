using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private readonly SpeechToTextModelCatalog _modelCatalog;
    private readonly CaptureSessionStorage _sessionStorage;
    private readonly AppSettingsStore _settingsStore;
    private readonly ITranscriptionService? _transcriptionService;
    private readonly string _sessionsRoot;
    private readonly List<CaptureSessionItemViewModel> _allSavedSessions = [];
    private string _outputPath;
    private long _totalBytesCaptured;
    private int _chunkCount;
    private bool _disposed;

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

    [ObservableProperty] private double overlayFontSize = 26;
    [ObservableProperty] private double overlayWidth = 720;
    [ObservableProperty] private double overlayHeight = 200;
    [ObservableProperty] private string overlayTheme = "Dark";
    [ObservableProperty] private double overlayOpacity = 0.88;

    [ObservableProperty] private string activeTab = "capture";
    [ObservableProperty] private SpeechToTextModelOption? selectedSpeechToTextModel;
    [ObservableProperty] private string sessionsDirectoryPath;
    [ObservableProperty] private string speechToTextStatus;
    [ObservableProperty] private CaptureSessionItemViewModel? selectedSavedSession;
    [ObservableProperty] private string sessionSearchText = string.Empty;
    [ObservableProperty] private string selectedSessionModelName = "Unknown";
    [ObservableProperty] private string selectedSessionDeviceName = "Unknown";
    [ObservableProperty] private string selectedSessionLanguage = "en";
    [ObservableProperty] private string selectedSessionDurationText = "0.0 s";
    [ObservableProperty] private string selectedSessionAudioPath = string.Empty;
    [ObservableProperty] private string selectedSessionTranscriptPath = string.Empty;

    public ObservableCollection<AudioDeviceItemViewModel> Devices { get; }
    public ObservableCollection<SpeechToTextModelOption> SpeechToTextModels { get; }
    public ObservableCollection<CaptureSessionItemViewModel> SavedSessions { get; }
    public ObservableCollection<SavedTranscriptEntryViewModel> SelectedSessionTranscriptEntries { get; }

    public bool HasDevices => Devices.Count > 0;
    public bool CanStartCapture => !IsBusy && !IsCapturing && HasDevices;
    public bool CanStopCapture => !IsBusy && IsCapturing;
    public bool HasRuntimeLog => !string.IsNullOrWhiteSpace(RuntimeLog);
    public bool HasTranscript => !string.IsNullOrWhiteSpace(PartialTranscript) || !string.IsNullOrWhiteSpace(FinalTranscript);
    public bool HasPartialTranslation => !string.IsNullOrWhiteSpace(PartialTranslatedTranscript);
    public bool HasFinalTranslation => !string.IsNullOrWhiteSpace(FinalTranslatedTranscript);
    public string OverlayToggleLabel => IsOverlayVisible ? "Hide Overlay" : "Show Overlay";
    public bool IsCaptureTabActive => string.Equals(ActiveTab, "capture", StringComparison.OrdinalIgnoreCase);
    public bool IsOverlayTabActive => string.Equals(ActiveTab, "overlay", StringComparison.OrdinalIgnoreCase);
    public bool HasSavedSessions => SavedSessions.Count > 0;
    public bool NoSavedSessions => !HasSavedSessions;
    public bool HasSelectedSessions => SavedSessions.Any(session => session.IsSelected);
    public bool HasSelectedSavedSession => SelectedSavedSession is not null;
    public bool HasSelectedSessionTranscriptEntries => SelectedSessionTranscriptEntries.Count > 0;
    public bool NoSelectedSessionTranscriptEntries => !HasSelectedSessionTranscriptEntries;
    public string SelectedSessionEntryCountText => $"{SelectedSessionTranscriptEntries.Count} transcript entr{(SelectedSessionTranscriptEntries.Count == 1 ? "y" : "ies")}";

    public MainWindowViewModel(
        AudioCaptureDebugSession session,
        SpeechToTextModelCatalog modelCatalog,
        CaptureSessionStorage sessionStorage,
        AppSettingsStore settingsStore,
        ITranscriptionService? transcriptionService = null)
    {
        _session = session;
        _modelCatalog = modelCatalog;
        _sessionStorage = sessionStorage;
        _settingsStore = settingsStore;
        _transcriptionService = transcriptionService;
        _session.ChunkObserved += OnChunkObserved;

        _outputPath = Path.Combine(Environment.CurrentDirectory, "system-audio.wav");
        _sessionsRoot = _sessionStorage.GetSessionsRoot();

        Devices = [];
        SpeechToTextModels = [];
        SavedSessions = [];
        SelectedSessionTranscriptEntries = [];

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
        sessionsDirectoryPath = _sessionsRoot;
        speechToTextStatus = _transcriptionService is VoskTranscriptionService vosk
            ? $"Active model: {vosk.CurrentModelName}"
            : "Speech-to-text provider ready.";

        LoadSpeechToTextModels();
        LoadSavedSessions();

        _session.TranscriptPreviewUpdated += OnTranscriptPreviewUpdated;
        PropertyChanged += OnPropertyChanged;
        _ = LoadDevicesAsync();
    }

    public MainWindowViewModel()
        : this(
            CreateDesignTimeSession(),
            new SpeechToTextModelCatalog(),
            new CaptureSessionStorage(),
            new AppSettingsStore())
    {
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand(CanExecute = nameof(CanStartCapture))]
    private async Task StartCaptureAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            _outputPath = _sessionStorage.CreateSessionOutputPath();
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
            OutputFilePath = _outputPath;

            await _session.StartAsync(
                string.IsNullOrWhiteSpace(SelectedDevice?.Id) ? null : SelectedDevice.Id,
                SelectedDeviceName,
                _outputPath);

            LoadSavedSessions();
            SelectedSavedSession = SavedSessions.FirstOrDefault(session =>
                string.Equals(session.AudioPath, _outputPath, StringComparison.OrdinalIgnoreCase));

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

    [RelayCommand]
    private void ClearSavedSessions()
    {
        var deletedCount = _sessionStorage.ClearAllSessions();
        LoadSavedSessions();
        RuntimeLog = deletedCount == 0
            ? $"No saved sessions found in {_sessionsRoot}"
            : $"Deleted {deletedCount} saved session(s) from {_sessionsRoot}";
    }

    [RelayCommand]
    private void RefreshSavedSessions()
    {
        LoadSavedSessions();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSessions))]
    private void DeleteSelectedSessions()
    {
        var selectedPaths = SavedSessions
            .Where(session => session.IsSelected)
            .Select(session => session.DirectoryPath)
            .ToList();

        var deletedCount = _sessionStorage.DeleteSessions(selectedPaths);
        LoadSavedSessions();
        LoadSelectedSessionTranscript();
        RuntimeLog = deletedCount == 0
            ? "No selected sessions were deleted."
            : $"Deleted {deletedCount} selected session(s).";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void OpenSelectedSessionFolder()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{SelectedSavedSession.DirectoryPath}\"",
            UseShellExecute = true,
        };

        Process.Start(psi);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void PlaySelectedSessionAudio()
    {
        if (SelectedSavedSession is null || !File.Exists(SelectedSavedSession.AudioPath))
        {
            RuntimeLog = "Selected session audio file was not found.";
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = SelectedSavedSession.AudioPath,
            UseShellExecute = true,
        };

        Process.Start(psi);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void DeleteCurrentSession()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var deletedCount = _sessionStorage.DeleteSessions([SelectedSavedSession.DirectoryPath]);
        LoadSavedSessions();
        RuntimeLog = deletedCount == 0
            ? "Current session was not deleted."
            : "Current session deleted.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void ExportSelectedSessionTranscriptAsTxt()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var transcriptEntries = _sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath);
        var exportPath = Path.Combine(SelectedSavedSession.DirectoryPath, "transcript.txt");
        var lines = transcriptEntries.SelectMany(entry =>
        {
            var result = new List<string> { $"[{entry.UpdatedAt:yyyy-MM-dd HH:mm:ss}]" };
            if (!string.IsNullOrWhiteSpace(entry.PartialText)) result.Add($"Partial: {entry.PartialText}");
            if (!string.IsNullOrWhiteSpace(entry.PartialTranslatedText)) result.Add($"Partial Translation: {entry.PartialTranslatedText}");
            if (!string.IsNullOrWhiteSpace(entry.FinalText)) result.Add($"Final: {entry.FinalText}");
            if (!string.IsNullOrWhiteSpace(entry.FinalTranslatedText)) result.Add($"Final Translation: {entry.FinalTranslatedText}");
            result.Add(string.Empty);
            return result;
        });

        File.WriteAllLines(exportPath, lines);
        RuntimeLog = $"Exported transcript txt to {exportPath}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSavedSession))]
    private void ExportSelectedSessionTranscriptAsJson()
    {
        if (SelectedSavedSession is null)
        {
            return;
        }

        var exportPath = Path.Combine(SelectedSavedSession.DirectoryPath, "transcript-export.json");
        File.Copy(SelectedSavedSession.TranscriptPath, exportPath, true);
        RuntimeLog = $"Exported transcript json to {exportPath}";
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

    private void LoadSpeechToTextModels()
    {
        SpeechToTextModels.Clear();
        foreach (var model in _modelCatalog.GetAvailableModels())
        {
            SpeechToTextModels.Add(model);
        }

        var selectedModelName = _settingsStore.Load().SpeechToText.SelectedModel;
        SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(model =>
                string.Equals(model.Name, selectedModelName, StringComparison.OrdinalIgnoreCase))
            ?? SpeechToTextModels.FirstOrDefault(model =>
                string.Equals(model.Name, "default", StringComparison.OrdinalIgnoreCase))
            ?? SpeechToTextModels.FirstOrDefault();

        if (SelectedSpeechToTextModel is not null)
        {
            SpeechToTextStatus = $"Selected model: {SelectedSpeechToTextModel.Name}";
        }
    }

    private void LoadSavedSessions()
    {
        var preferredSelectedPath = SelectedSavedSession?.DirectoryPath;

        foreach (var existingSession in _allSavedSessions)
        {
            existingSession.PropertyChanged -= OnSavedSessionPropertyChanged;
        }

        _allSavedSessions.Clear();
        foreach (var session in _sessionStorage.GetSessions())
        {
            var item = new CaptureSessionItemViewModel(session);
            item.PropertyChanged += OnSavedSessionPropertyChanged;
            _allSavedSessions.Add(item);
        }

        ApplySavedSessionsFilter(preferredSelectedPath);
    }

    partial void OnSessionSearchTextChanged(string value)
    {
        ApplySavedSessionsFilter(SelectedSavedSession?.DirectoryPath);
    }

    private void ApplySavedSessionsFilter(string? preferredSelectedPath)
    {
        SavedSessions.Clear();

        var filtered = string.IsNullOrWhiteSpace(SessionSearchText)
            ? _allSavedSessions
            : _allSavedSessions.Where(session =>
                session.SessionId.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.AudioPath.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.CreatedAtText.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var session in filtered)
        {
            SavedSessions.Add(session);
        }

        SelectedSavedSession = SavedSessions.FirstOrDefault(session =>
                string.Equals(session.DirectoryPath, preferredSelectedPath, StringComparison.OrdinalIgnoreCase))
            ?? SavedSessions.FirstOrDefault();

        LoadSelectedSessionTranscript();

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSavedSessions)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSavedSessions)));
        DeleteSelectedSessionsCommand.NotifyCanExecuteChanged();
    }

    private void LoadSelectedSessionTranscript()
    {
        SelectedSessionTranscriptEntries.Clear();

        if (SelectedSavedSession is null)
        {
            SelectedSessionModelName = "Unknown";
            SelectedSessionDeviceName = "Unknown";
            SelectedSessionLanguage = "en";
            SelectedSessionDurationText = "0.0 s";
            SelectedSessionAudioPath = string.Empty;
            SelectedSessionTranscriptPath = string.Empty;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSavedSession)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessionTranscriptEntries)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSelectedSessionTranscriptEntries)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedSessionEntryCountText)));
            OpenSelectedSessionFolderCommand.NotifyCanExecuteChanged();
            PlaySelectedSessionAudioCommand.NotifyCanExecuteChanged();
            DeleteCurrentSessionCommand.NotifyCanExecuteChanged();
            ExportSelectedSessionTranscriptAsTxtCommand.NotifyCanExecuteChanged();
            ExportSelectedSessionTranscriptAsJsonCommand.NotifyCanExecuteChanged();
            return;
        }

        var metadata = _sessionStorage.GetSessionMetadata(SelectedSavedSession.MetadataPath);
        SelectedSessionModelName = metadata?.ModelName ?? "Unknown";
        SelectedSessionDeviceName = metadata?.DeviceName ?? "Unknown";
        SelectedSessionLanguage = metadata?.Language ?? "en";
        SelectedSessionDurationText = $"{(metadata?.DurationSeconds ?? 0):0.0} s";
        SelectedSessionAudioPath = SelectedSavedSession.AudioPath;
        SelectedSessionTranscriptPath = SelectedSavedSession.TranscriptPath;

        foreach (var entry in _sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath))
        {
            SelectedSessionTranscriptEntries.Add(new SavedTranscriptEntryViewModel(entry));
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSavedSession)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessionTranscriptEntries)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSelectedSessionTranscriptEntries)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedSessionEntryCountText)));
        OpenSelectedSessionFolderCommand.NotifyCanExecuteChanged();
        PlaySelectedSessionAudioCommand.NotifyCanExecuteChanged();
        DeleteCurrentSessionCommand.NotifyCanExecuteChanged();
        ExportSelectedSessionTranscriptAsTxtCommand.NotifyCanExecuteChanged();
        ExportSelectedSessionTranscriptAsJsonCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadDevicesAsync()
    {
        var devices = await _session.GetAvailableDevicesAsync();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(new AudioDeviceItemViewModel(device));
            }

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

            if (SelectedSavedSession is not null
                && string.Equals(SelectedSavedSession.AudioPath, _outputPath, StringComparison.OrdinalIgnoreCase))
            {
                LoadSelectedSessionTranscript();
            }
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

        if (e.PropertyName == nameof(SelectedSpeechToTextModel) && SelectedSpeechToTextModel is not null)
        {
            var settings = _settingsStore.Load();
            settings.SpeechToText.SelectedModel = SelectedSpeechToTextModel.Name;
            _settingsStore.Save(settings);
            SpeechToTextStatus = $"Selected model: {SelectedSpeechToTextModel.Name}";
        }

        if (e.PropertyName == nameof(SelectedSavedSession))
        {
            LoadSelectedSessionTranscript();
        }

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

    private void OnSavedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureSessionItemViewModel.IsSelected))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedSessions)));
            DeleteSelectedSessionsCommand.NotifyCanExecuteChanged();
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
            new Sublingual.Application.Audio.TranslateTranscriptUseCase(new MockTranslationService()),
            new CaptureSessionStorage());
    }
}

public sealed class AudioDeviceItemViewModel(AudioDevice device)
{
    public string Id { get; } = device.Id;
    public string Name { get; } = device.Name;
    public bool IsDefault { get; } = device.IsDefault;
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;
}

public sealed partial class CaptureSessionItemViewModel : ObservableObject
{
    public CaptureSessionItemViewModel(CaptureSessionRecord record)
    {
        SessionId = record.SessionId;
        DirectoryPath = record.DirectoryPath;
        AudioPath = record.AudioPath;
        TranscriptPath = record.TranscriptPath;
        MetadataPath = record.MetadataPath;
        CreatedAt = record.CreatedAt;
    }

    [ObservableProperty] private bool isSelected;

    public string SessionId { get; }
    public string DirectoryPath { get; }
    public string AudioPath { get; }
    public string TranscriptPath { get; }
    public string MetadataPath { get; }
    public DateTimeOffset CreatedAt { get; }
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

public sealed class SavedTranscriptEntryViewModel
{
    public SavedTranscriptEntryViewModel(SavedTranscriptEntry entry)
    {
        PartialText = entry.PartialText;
        PartialTranslatedText = entry.PartialTranslatedText;
        FinalText = entry.FinalText;
        FinalTranslatedText = entry.FinalTranslatedText;
        UpdatedAt = entry.UpdatedAt;
    }

    public string PartialText { get; }
    public string PartialTranslatedText { get; }
    public string FinalText { get; }
    public string FinalTranslatedText { get; }
    public DateTimeOffset UpdatedAt { get; }
    public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public bool HasPartial => !string.IsNullOrWhiteSpace(PartialText);
    public bool HasPartialTranslation => !string.IsNullOrWhiteSpace(PartialTranslatedText);
    public bool HasFinal => !string.IsNullOrWhiteSpace(FinalText);
    public bool HasFinalTranslation => !string.IsNullOrWhiteSpace(FinalTranslatedText);
}
