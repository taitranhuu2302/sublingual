using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.App.Services.Translation;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private readonly SpeechToTextModelCatalog _modelCatalog;
    private readonly SpeechToTextModelImporter _modelImporter;
    private readonly CaptureSessionStorage _sessionStorage;
    private readonly AppSettingsStore _settingsStore;
    private readonly ITranscriptionService? _transcriptionService;
    private readonly ITranslationService? _translationService;
    private readonly string _sessionsRoot;
    private readonly List<CaptureSessionItemViewModel> _allSavedSessions = [];
    private string _outputPath;
    private long _totalBytesCaptured;
    private int _chunkCount;
    private bool _disposed;
    private readonly Queue<double> _waveformSamples = new();
    private const int WaveformSampleCapacity = 24;
    private const int RuntimeLogLineLimit = 180;

    public Action? ToggleOverlayAction { get; set; }
    public Action? EnsureOverlayVisibleAction { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelZipFileAsync { get; set; }

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
    [ObservableProperty] private double overlayLineHeight = 1.35;
    [ObservableProperty] private double overlayWidth = 720;
    [ObservableProperty] private double overlayHeight = 200;
    [ObservableProperty] private string overlayTheme = "Dark";
    [ObservableProperty] private double overlayOpacity = 0.88;
    [ObservableProperty] private bool overlayShowTranslation = true;
    [ObservableProperty] private string overlayLineHeightPreset = "Default";

    [ObservableProperty] private string activeTab = "capture";
    [ObservableProperty] private SpeechToTextModelOption? selectedSpeechToTextModel;
    [ObservableProperty] private string sessionsDirectoryPath;
    [ObservableProperty] private string speechToTextStatus;
    [ObservableProperty] private string selectedTranslationFactory = TranslationFactories.FallbackChain;
    [ObservableProperty] private string selectedSourceLanguage = "en";
    [ObservableProperty] private string selectedTargetLanguage = "vi";
    [ObservableProperty] private string translationPrimaryProvider = TranslationProviders.GoogleTranslateFreeApi;
    [ObservableProperty] private string translationSecondaryProvider = TranslationProviders.LibreTranslate;
    [ObservableProperty] private bool googleTranslateFreeApiEnabled = true;
    [ObservableProperty] private string googleTranslateFreeApiEndpoint = "https://translate.googleapis.com/translate_a/single";
    [ObservableProperty] private bool libreTranslateEnabled = true;
    [ObservableProperty] private string libreTranslateEndpoint = "https://libretranslate.com/translate";
    [ObservableProperty] private string libreTranslateApiKey = string.Empty;
    [ObservableProperty] private bool translatePartials;
    [ObservableProperty] private string translationStatus = string.Empty;
    [ObservableProperty] private string translationTestSourceText = "Hello, how are you today?";
    [ObservableProperty] private string translationTestSourceLanguage = "en";
    [ObservableProperty] private string translationTestTargetLanguage = "vi";
    [ObservableProperty] private string translationTestResult = string.Empty;
    [ObservableProperty] private string translationTestError = string.Empty;
    [ObservableProperty] private bool isTestingTranslation;
    [ObservableProperty] private CaptureSessionItemViewModel? selectedSavedSession;
    [ObservableProperty] private string activeSessionsPage = "list";
    [ObservableProperty] private string sessionSearchText = string.Empty;
    [ObservableProperty] private string selectedSessionModelName = "Unknown";
    [ObservableProperty] private string selectedSessionDeviceName = "Unknown";
    [ObservableProperty] private string selectedSessionLanguage = "en";
    [ObservableProperty] private string selectedSessionDurationText = "0.0 s";
    [ObservableProperty] private string selectedSessionAudioPath = string.Empty;
    [ObservableProperty] private string selectedSessionTranscriptPath = string.Empty;
    [ObservableProperty] private int sessionsPageIndex;
    [ObservableProperty] private string activeSettingsTab = "general";
    [ObservableProperty] private bool areAllSessionsSelected;

    public ObservableCollection<AudioDeviceItemViewModel> Devices { get; }
    public ObservableCollection<SpeechToTextModelOption> SpeechToTextModels { get; }
    public ObservableCollection<LanguageOptionViewModel> TranslationLanguages { get; }
    public ObservableCollection<CaptureSessionItemViewModel> SavedSessions { get; }
    public ObservableCollection<SavedTranscriptEntryViewModel> SelectedSessionTranscriptEntries { get; }
    public ObservableCollection<AudioLevelBarViewModel> AudioLevelBars { get; }

    public bool HasDevices => Devices.Count > 0;
    public bool CanStartCapture => !IsBusy && !IsCapturing && HasDevices;
    public bool CanStopCapture => !IsBusy && IsCapturing;
    public bool HasRuntimeLog => !string.IsNullOrWhiteSpace(RuntimeLog);
    public bool HasTranscript => !string.IsNullOrWhiteSpace(PartialTranscript) || !string.IsNullOrWhiteSpace(FinalTranscript);
    public bool HasPartialTranslation => !string.IsNullOrWhiteSpace(PartialTranslatedTranscript);
    public bool HasFinalTranslation => !string.IsNullOrWhiteSpace(FinalTranslatedTranscript);
    public string OverlayToggleLabel => IsOverlayVisible ? "Hide Overlay" : "Show Overlay";
    public bool IsCaptureTabActive => string.Equals(ActiveTab, "capture", StringComparison.OrdinalIgnoreCase);
    public bool IsSessionsTabActive => string.Equals(ActiveTab, "sessions", StringComparison.OrdinalIgnoreCase);
    public bool IsSettingsTabActive => string.Equals(ActiveTab, "settings", StringComparison.OrdinalIgnoreCase);
    public bool IsGeneralSettingsTabActive => string.Equals(ActiveSettingsTab, "general", StringComparison.OrdinalIgnoreCase);
    public bool IsSpeechSettingsTabActive => string.Equals(ActiveSettingsTab, "speech", StringComparison.OrdinalIgnoreCase);
    public bool IsTranslationSettingsTabActive => string.Equals(ActiveSettingsTab, "translation", StringComparison.OrdinalIgnoreCase);
    public bool IsOverlaySettingsTabActive => string.Equals(ActiveSettingsTab, "overlay", StringComparison.OrdinalIgnoreCase);
    public bool IsFallbackTranslationFactory => string.Equals(SelectedTranslationFactory, TranslationFactories.FallbackChain, StringComparison.OrdinalIgnoreCase);
    public bool IsRealtimeTranslationEnabled => !string.Equals(SelectedSourceLanguage, SelectedTargetLanguage, StringComparison.OrdinalIgnoreCase);
    public bool CanTestTranslation => !IsTestingTranslation && !string.IsNullOrWhiteSpace(TranslationTestSourceText);
    public bool HasTranslationTestResult => !string.IsNullOrWhiteSpace(TranslationTestResult);
    public bool HasTranslationTestError => !string.IsNullOrWhiteSpace(TranslationTestError);
    public bool HasSavedSessions => SavedSessions.Count > 0;
    public bool NoSavedSessions => !HasSavedSessions;
    public bool HasSelectedSessions => SavedSessions.Any(session => session.IsSelected);
    public bool HasSelectedSavedSession => SelectedSavedSession is not null;
    public bool IsSessionsListPageActive => string.Equals(ActiveSessionsPage, "list", StringComparison.OrdinalIgnoreCase);
    public bool IsSessionsDetailPageActive => string.Equals(ActiveSessionsPage, "detail", StringComparison.OrdinalIgnoreCase);
    public bool HasSelectedSessionTranscriptEntries => SelectedSessionTranscriptEntries.Count > 0;
    public bool NoSelectedSessionTranscriptEntries => !HasSelectedSessionTranscriptEntries;
    public string SelectedSessionEntryCountText => $"{SelectedSessionTranscriptEntries.Count} transcript entr{(SelectedSessionTranscriptEntries.Count == 1 ? "y" : "ies")}";
    public int SessionsPageSize => 8;
    public int SessionsPageCount => Math.Max(1, (int)Math.Ceiling((double)GetFilteredSessions().Count / SessionsPageSize));
    public bool CanGoToPreviousSessionsPage => SessionsPageIndex > 0;
    public bool CanGoToNextSessionsPage => SessionsPageIndex + 1 < SessionsPageCount;
    public string SessionsPageText => $"{Math.Min(SessionsPageIndex + 1, SessionsPageCount)}/{SessionsPageCount}";
    public double OverlayLineHeightPreviewPixels => 16 * OverlayLineHeight;
    public bool IsCompactOverlayLineHeightPreset => string.Equals(OverlayLineHeightPreset, "Compact", StringComparison.OrdinalIgnoreCase);
    public bool IsDefaultOverlayLineHeightPreset => string.Equals(OverlayLineHeightPreset, "Default", StringComparison.OrdinalIgnoreCase);
    public bool IsRelaxedOverlayLineHeightPreset => string.Equals(OverlayLineHeightPreset, "Relaxed", StringComparison.OrdinalIgnoreCase);

    public MainWindowViewModel(
        AudioCaptureDebugSession session,
        SpeechToTextModelCatalog modelCatalog,
        SpeechToTextModelImporter modelImporter,
        CaptureSessionStorage sessionStorage,
        AppSettingsStore settingsStore,
        ITranscriptionService? transcriptionService = null,
        ITranslationService? translationService = null)
    {
        _session = session;
        _modelCatalog = modelCatalog;
        _modelImporter = modelImporter;
        _sessionStorage = sessionStorage;
        _settingsStore = settingsStore;
        _transcriptionService = transcriptionService;
        _translationService = translationService;
        _session.ChunkObserved += OnChunkObserved;

        _outputPath = Path.Combine(Environment.CurrentDirectory, "system-audio.wav");
        _sessionsRoot = _sessionStorage.GetSessionsRoot();

        Devices = [];
        SpeechToTextModels = [];
        TranslationLanguages =
        [
            new LanguageOptionViewModel("English", "en"),
            new LanguageOptionViewModel("Chinese", "zh"),
            new LanguageOptionViewModel("Vietnamese", "vi"),
        ];
        SavedSessions = [];
        SelectedSessionTranscriptEntries = [];
        AudioLevelBars = [];
        for (var i = 0; i < WaveformSampleCapacity; i++)
        {
            AudioLevelBars.Add(new AudioLevelBarViewModel());
        }

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
        LoadTranslationSettings();
        LoadSavedSessions();

        _session.TranscriptPreviewUpdated += OnTranscriptPreviewUpdated;
        PropertyChanged += OnPropertyChanged;
        _ = LoadDevicesAsync();
    }

    public MainWindowViewModel()
        : this(
            CreateDesignTimeSession(),
            new SpeechToTextModelCatalog(),
            new SpeechToTextModelImporter(new SpeechToTextModelCatalog()),
            new CaptureSessionStorage(),
            new AppSettingsStore(),
            translationService: new ConfigurableTranslationService(
                [
                    new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                    new LibreTranslateTranslationProvider(new HttpClient()),
                ],
                new AppSettingsStore()
            ))
    {
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        ActiveTab = tab;

        // Sessions has internal navigation (list/detail). Default to list when entering.
        if (string.Equals(tab, "sessions", StringComparison.OrdinalIgnoreCase))
        {
            ActiveSessionsPage = "list";
        }
    }

    [RelayCommand]
    private void OpenSessionDetail(CaptureSessionItemViewModel session)
    {
        SelectedSavedSession = session;
        ActiveSessionsPage = "detail";
    }

    [RelayCommand]
    private void BackToSessionsList()
    {
        ActiveSessionsPage = "list";
    }

    [RelayCommand]
    private void SelectSettingsTab(string tab) => ActiveSettingsTab = tab;

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

            EnsureOverlayVisibleAction?.Invoke();

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
    private async Task ImportSpeechToTextModelAsync()
    {
        if (PickSpeechToTextModelDirectoryAsync is null)
        {
            RuntimeLog = "Model import is not available in the current UI context.";
            return;
        }

        var selectedDirectory = await PickSpeechToTextModelDirectoryAsync();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            RuntimeLog = "Model import cancelled.";
            return;
        }

        await RunBusyOperationAsync(() => ImportSpeechToTextModelCoreAsync(selectedDirectory));
    }

    [RelayCommand]
    private async Task ImportSpeechToTextModelZipAsync()
    {
        if (PickSpeechToTextModelZipFileAsync is null)
        {
            RuntimeLog = "Zip model import is not available in the current UI context.";
            return;
        }

        var selectedFile = await PickSpeechToTextModelZipFileAsync();
        if (string.IsNullOrWhiteSpace(selectedFile))
        {
            RuntimeLog = "Zip model import cancelled.";
            return;
        }

        await RunBusyOperationAsync(() => ImportSpeechToTextModelZipCoreAsync(selectedFile));
    }

    [RelayCommand]
    private void OpenSpeechToTextModelsFolder()
    {
        var modelsRoot = _modelCatalog.GetManagedModelsRoot();
        Directory.CreateDirectory(modelsRoot);

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{modelsRoot}\"",
                UseShellExecute = true,
            }
            : new ProcessStartInfo
            {
                FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                Arguments = $"\"{modelsRoot}\"",
                UseShellExecute = true,
            };

        Process.Start(psi);
    }

    [RelayCommand(CanExecute = nameof(CanTestTranslation))]
    private async Task TestTranslationAsync()
    {
        if (_translationService is null)
        {
            TranslationTestError = "Translation service is not available.";
            TranslationTestResult = string.Empty;
            return;
        }

        try
        {
            IsTestingTranslation = true;
            TranslationTestError = string.Empty;
            TranslationTestResult = string.Empty;

            var sourceLanguage = string.IsNullOrWhiteSpace(TranslationTestSourceLanguage)
                ? "en"
                : TranslationTestSourceLanguage.Trim();
            var targetLanguage = string.IsNullOrWhiteSpace(TranslationTestTargetLanguage)
                ? "vi"
                : TranslationTestTargetLanguage.Trim();

            var result = await _translationService.TranslateAsync(
                new TranslationRequest(TranslationTestSourceText.Trim(), sourceLanguage, targetLanguage)
            );

            TranslationTestResult = result.TranslatedText;
            TranslationTestError = string.Empty;
        }
        catch (Exception ex)
        {
            TranslationTestResult = string.Empty;
            TranslationTestError = ex.Message;
        }
        finally
        {
            IsTestingTranslation = false;
            TestTranslationCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestResult)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestError)));
        }
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

    [RelayCommand]
    private void ToggleSelectAllSessions()
    {
        var target = !AreAllSessionsSelected;
        foreach (var session in SavedSessions)
        {
            session.IsSelected = target;
        }

        AreAllSessionsSelected = target;
    }

    [RelayCommand]
    private void ToggleSessionSelection(CaptureSessionItemViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        session.IsSelected = !session.IsSelected;
    }

    [RelayCommand]
    private void SetOverlayLineHeightPreset(string preset)
    {
        OverlayLineHeightPreset = preset;
        OverlayLineHeight = preset switch
        {
            "Compact" => 1.05,
            "Relaxed" => 1.65,
            _ => 1.35,
        };
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousSessionsPage))]
    private void PreviousSessionsPage()
    {
        if (!CanGoToPreviousSessionsPage)
        {
            return;
        }

        SessionsPageIndex -= 1;
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextSessionsPage))]
    private void NextSessionsPage()
    {
        if (!CanGoToNextSessionsPage)
        {
            return;
        }

        SessionsPageIndex += 1;
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

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{SelectedSavedSession.DirectoryPath}\"",
                UseShellExecute = true,
            }
            : new ProcessStartInfo
            {
                FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
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
        ActiveSessionsPage = "list";
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

        var transcriptEntries = BuildExportTranscriptEntries(_sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath));
        var exportPath = Path.Combine(SelectedSavedSession.DirectoryPath, "transcript.txt");
        var lines = transcriptEntries.SelectMany(entry =>
        {
            var result = new List<string> { $"[{entry.UpdatedAt:yyyy-MM-dd HH:mm:ss}]" };
            if (!string.IsNullOrWhiteSpace(entry.PartialText)) result.Add($"Partial: {entry.PartialText}");
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
        var transcriptEntries = BuildExportTranscriptEntries(_sessionStorage.GetTranscriptEntries(SelectedSavedSession.TranscriptPath));
        var json = System.Text.Json.JsonSerializer.Serialize(transcriptEntries, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(exportPath, json);
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
            UpdateSpeechToTextStatus();
            return;
        }

        SpeechToTextStatus = "No local speech model found.";
    }

    private Task ImportSpeechToTextModelCoreAsync(string selectedDirectory)
    {
        var importedPath = _modelImporter.ImportFromDirectory(selectedDirectory);
        LoadSpeechToTextModels();

        var importedModelName = Path.GetFileName(importedPath);
        SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(model =>
            string.Equals(model.Name, importedModelName, StringComparison.OrdinalIgnoreCase));

        SpeechToTextStatus = SelectedSpeechToTextModel is null
            ? "Model imported but could not be selected."
            : BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
        AppendRuntimeLog($"Imported speech-to-text model from {selectedDirectory}");

        return Task.CompletedTask;
    }

    private Task ImportSpeechToTextModelZipCoreAsync(string selectedFile)
    {
        var importedPath = _modelImporter.ImportFromZip(selectedFile);
        LoadSpeechToTextModels();

        var importedModelName = Path.GetFileName(importedPath);
        SelectedSpeechToTextModel = SpeechToTextModels.FirstOrDefault(model =>
            string.Equals(model.Name, importedModelName, StringComparison.OrdinalIgnoreCase));

        SpeechToTextStatus = SelectedSpeechToTextModel is null
            ? "Zip model imported but could not be selected."
            : BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
        AppendRuntimeLog($"Imported zipped speech-to-text model from {selectedFile}");

        return Task.CompletedTask;
    }

    private void LoadTranslationSettings()
    {
        var settings = _settingsStore.Load().Translation;
        SelectedTranslationFactory = NormalizeTranslationFactory(settings.Factory);

        var providerOrder = settings.ProviderOrder
            .Where(static provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        TranslationPrimaryProvider = NormalizeTranslationProvider(
            providerOrder.ElementAtOrDefault(0),
            TranslationProviders.GoogleTranslateFreeApi
        );

        TranslationSecondaryProvider = NormalizeTranslationProvider(
            providerOrder.FirstOrDefault(provider =>
                !string.Equals(provider, TranslationPrimaryProvider, StringComparison.OrdinalIgnoreCase)),
            string.Equals(TranslationPrimaryProvider, TranslationProviders.GoogleTranslateFreeApi, StringComparison.OrdinalIgnoreCase)
                ? TranslationProviders.LibreTranslate
                : TranslationProviders.GoogleTranslateFreeApi
        );

        GoogleTranslateFreeApiEnabled = settings.GoogleTranslateFreeApi.Enabled;
        GoogleTranslateFreeApiEndpoint = string.IsNullOrWhiteSpace(settings.GoogleTranslateFreeApi.Endpoint)
            ? "https://translate.googleapis.com/translate_a/single"
            : settings.GoogleTranslateFreeApi.Endpoint;

        LibreTranslateEnabled = settings.LibreTranslate.Enabled;
        LibreTranslateEndpoint = string.IsNullOrWhiteSpace(settings.LibreTranslate.Endpoint)
            ? "https://libretranslate.com/translate"
            : settings.LibreTranslate.Endpoint;
        LibreTranslateApiKey = settings.LibreTranslate.ApiKey ?? string.Empty;
        TranslatePartials = settings.TranslatePartials;

        TranslationStatus = BuildTranslationStatus();
        UpdateSpeechToTextStatus();
    }

    private void SaveTranslationSettings()
    {
        var settings = _settingsStore.Load();
        settings.Translation.Factory = NormalizeTranslationFactory(SelectedTranslationFactory);
        settings.Translation.ProviderOrder = BuildTranslationProviderOrder();
        settings.Translation.GoogleTranslateFreeApi.Enabled = GoogleTranslateFreeApiEnabled;
        settings.Translation.GoogleTranslateFreeApi.Endpoint = GoogleTranslateFreeApiEndpoint.Trim();
        settings.Translation.LibreTranslate.Enabled = LibreTranslateEnabled;
        settings.Translation.LibreTranslate.Endpoint = LibreTranslateEndpoint.Trim();
        settings.Translation.LibreTranslate.ApiKey = LibreTranslateApiKey.Trim();
        settings.Translation.TranslatePartials = TranslatePartials;
        _settingsStore.Save(settings);
        TranslationStatus = BuildTranslationStatus();
        UpdateSpeechToTextStatus();
    }

    private List<string> BuildTranslationProviderOrder()
    {
        var providers = new List<string>();
        foreach (var provider in new[] { TranslationPrimaryProvider, TranslationSecondaryProvider })
        {
            var normalized = NormalizeTranslationProvider(provider, TranslationProviders.GoogleTranslateFreeApi);
            if (!providers.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                providers.Add(normalized);
            }
        }

        return providers;
    }

    private string BuildTranslationStatus()
    {
        var factory = NormalizeTranslationFactory(SelectedTranslationFactory);
        if (string.Equals(factory, TranslationFactories.FallbackChain, StringComparison.OrdinalIgnoreCase))
        {
            return $"Fallback chain: {string.Join(" -> ", BuildTranslationProviderOrder())} | {(TranslatePartials ? "partials on" : "final-only")}";
        }

        return $"Active provider: {factory} | {(TranslatePartials ? "partials on" : "final-only")}";
    }

    private static string NormalizeTranslationFactory(string? factory)
    {
        return string.Equals(factory, TranslationProviders.GoogleTranslateFreeApi, StringComparison.OrdinalIgnoreCase)
            ? TranslationProviders.GoogleTranslateFreeApi
            : string.Equals(factory, TranslationProviders.LibreTranslate, StringComparison.OrdinalIgnoreCase)
                ? TranslationProviders.LibreTranslate
                : TranslationFactories.FallbackChain;
    }

    private static string NormalizeTranslationProvider(string? provider, string fallback)
    {
        return string.Equals(provider, TranslationProviders.LibreTranslate, StringComparison.OrdinalIgnoreCase)
            ? TranslationProviders.LibreTranslate
            : string.Equals(provider, TranslationProviders.GoogleTranslateFreeApi, StringComparison.OrdinalIgnoreCase)
                ? TranslationProviders.GoogleTranslateFreeApi
                : fallback;
    }

    private void UpdateSpeechToTextStatus()
    {
        if (SelectedSpeechToTextModel is null)
        {
            SpeechToTextStatus = "No local speech model found.";
            return;
        }

        SpeechToTextStatus = BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
    }

    private string BuildSpeechToTextStatus(string modelName)
    {
        var warning = GetSourceLanguageModelWarning(modelName, SelectedSourceLanguage);
        return string.IsNullOrWhiteSpace(warning)
            ? $"Selected model: {modelName}"
            : $"Selected model: {modelName} | Warning: {warning}";
    }

    private static string GetSourceLanguageModelWarning(string? modelName, string sourceLanguage)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return string.Empty;
        }

        var normalizedName = modelName.ToLowerInvariant();
        var expectedHints = sourceLanguage switch
        {
            "zh" => new[] { "zh", "cn", "chinese", "mandarin" },
            "vi" => new[] { "vi", "vn", "viet", "vietnamese" },
            _ => new[] { "en", "eng", "english" },
        };

        var conflictingHints = sourceLanguage switch
        {
            "zh" => new[] { "english", "vietnamese", "viet" },
            "vi" => new[] { "english", "chinese", "mandarin" },
            _ => new[] { "vietnamese", "viet", "chinese", "mandarin" },
        };

        var hasExpectedHint = expectedHints.Any(normalizedName.Contains);
        var hasConflictingHint = conflictingHints.Any(normalizedName.Contains);

        if (!hasExpectedHint && hasConflictingHint)
        {
            return $"source language `{sourceLanguage}` may not match the current Vosk model";
        }

        return string.Empty;
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

        AreAllSessionsSelected = false;

        ApplySavedSessionsFilter(preferredSelectedPath);
    }

    partial void OnSessionSearchTextChanged(string value)
    {
        SessionsPageIndex = 0;
        ApplySavedSessionsFilter(SelectedSavedSession?.DirectoryPath);
    }

    partial void OnSessionsPageIndexChanged(int value)
    {
        ApplySavedSessionsFilter(SelectedSavedSession?.DirectoryPath);
    }

    private void ApplySavedSessionsFilter(string? preferredSelectedPath)
    {
        SavedSessions.Clear();

        var filtered = GetFilteredSessions();
        var pageCount = Math.Max(1, (int)Math.Ceiling((double)filtered.Count / SessionsPageSize));

        if (SessionsPageIndex >= pageCount)
        {
            SessionsPageIndex = Math.Max(0, pageCount - 1);
            return;
        }

        var paged = filtered
            .Skip(SessionsPageIndex * SessionsPageSize)
            .Take(SessionsPageSize)
            .ToList();

        foreach (var session in paged)
        {
            SavedSessions.Add(session);
        }

        SelectedSavedSession = SavedSessions.FirstOrDefault(session =>
                string.Equals(session.DirectoryPath, preferredSelectedPath, StringComparison.OrdinalIgnoreCase))
            ?? SavedSessions.FirstOrDefault();

        LoadSelectedSessionTranscript();

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSavedSessions)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(NoSavedSessions)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SessionsPageCount)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SessionsPageText)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanGoToPreviousSessionsPage)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanGoToNextSessionsPage)));
        DeleteSelectedSessionsCommand.NotifyCanExecuteChanged();
        PreviousSessionsPageCommand.NotifyCanExecuteChanged();
        NextSessionsPageCommand.NotifyCanExecuteChanged();
    }

    private List<CaptureSessionItemViewModel> GetFilteredSessions()
    {
        return string.IsNullOrWhiteSpace(SessionSearchText)
            ? _allSavedSessions.ToList()
            : _allSavedSessions.Where(session =>
                session.SessionId.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.AudioPath.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase)
                || session.CreatedAtText.Contains(SessionSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
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
            var level = CalculateAudioLevelPercent(chunk);
            AudioLevel = level;
            PeakAudioLevel = Math.Max(PeakAudioLevel * 0.90, level);
            AudioLevelText = $"{level:0}%";
            PushWaveformSample(level);
            AppendRuntimeLog($"Chunk #{_chunkCount}: {chunk.Data.Length} B | {chunk.SampleRate} Hz | {chunk.Channels}ch | {chunk.BitsPerSample}bit | {chunk.Duration.TotalMilliseconds:F0}ms");
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
                TranscriptStatus = $"Updated {update.UpdatedAt:HH:mm:ss} | {update.TranslationProvider}{(update.TranslationCacheHit ? " | cache" : string.Empty)}";

            if (!string.IsNullOrWhiteSpace(update.TranslationDiagnostics))
            {
                AppendRuntimeLog($"Translation: {update.TranslationDiagnostics}");
            }

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
            UpdateSpeechToTextStatus();
        }

        if (e.PropertyName == nameof(SelectedSourceLanguage))
        {
            UpdateSpeechToTextStatus();
        }

        if (e.PropertyName == nameof(SelectedSavedSession))
        {
            LoadSelectedSessionTranscript();
        }

        if (e.PropertyName == nameof(ActiveSessionsPage))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSessionsListPageActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSessionsDetailPageActive)));
        }

        if (e.PropertyName == nameof(OverlayLineHeight))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(OverlayLineHeightPreviewPixels)));
        }

        if (e.PropertyName == nameof(OverlayLineHeightPreset))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCompactOverlayLineHeightPreset)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsDefaultOverlayLineHeightPreset)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsRelaxedOverlayLineHeightPreset)));
        }

        if (e.PropertyName == nameof(ActiveSettingsTab))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsGeneralSettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSpeechSettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsTranslationSettingsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsOverlaySettingsTabActive)));
        }

        if (e.PropertyName == nameof(SelectedTranslationFactory))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsFallbackTranslationFactory)));
            SaveTranslationSettings();
        }

        if (e.PropertyName is nameof(TranslationPrimaryProvider)
            or nameof(TranslationSecondaryProvider)
            or nameof(GoogleTranslateFreeApiEnabled)
            or nameof(GoogleTranslateFreeApiEndpoint)
            or nameof(LibreTranslateEnabled)
            or nameof(LibreTranslateEndpoint)
            or nameof(LibreTranslateApiKey)
            or nameof(TranslatePartials)
            or nameof(SelectedSourceLanguage)
            or nameof(SelectedTargetLanguage))
        {
            SaveTranslationSettings();
        }

        if (e.PropertyName is nameof(TranslationTestSourceText) or nameof(IsTestingTranslation))
        {
            TestTranslationCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(TranslationTestResult))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestResult)));
        }

        if (e.PropertyName == nameof(TranslationTestError))
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasTranslationTestError)));
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
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSessionsTabActive)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSettingsTabActive)));
        }
    }

    private static IReadOnlyList<SavedTranscriptEntry> BuildExportTranscriptEntries(IReadOnlyList<SavedTranscriptEntry> entries)
    {
        var cleaned = new List<SavedTranscriptEntry>();
        SavedTranscriptEntry? pendingPartial = null;

        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.FinalText) || !string.IsNullOrWhiteSpace(entry.FinalTranslatedText))
            {
                pendingPartial = null;

                var finalEntry = new SavedTranscriptEntry
                {
                    PartialText = string.Empty,
                    PartialTranslatedText = string.Empty,
                    FinalText = entry.FinalText,
                    FinalTranslatedText = entry.FinalTranslatedText,
                    UpdatedAt = entry.UpdatedAt,
                };

                var last = cleaned.LastOrDefault();
                var isDuplicateFinal = last is not null
                    && string.Equals(last.FinalText, finalEntry.FinalText, StringComparison.Ordinal)
                    && string.Equals(last.FinalTranslatedText, finalEntry.FinalTranslatedText, StringComparison.Ordinal);

                if (!isDuplicateFinal)
                {
                    cleaned.Add(finalEntry);
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.PartialText))
            {
                pendingPartial = new SavedTranscriptEntry
                {
                    PartialText = entry.PartialText,
                    PartialTranslatedText = string.Empty,
                    FinalText = string.Empty,
                    FinalTranslatedText = string.Empty,
                    UpdatedAt = entry.UpdatedAt,
                };
            }
        }

        if (cleaned.Count == 0 && pendingPartial is not null)
        {
            cleaned.Add(pendingPartial);
        }

        return cleaned;
    }

    private void OnSavedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CaptureSessionItemViewModel.IsSelected))
        {
            AreAllSessionsSelected = SavedSessions.Count > 0 && SavedSessions.All(session => session.IsSelected);
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
            new ConfigurableTranslationService(
                [
                    new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                    new LibreTranslateTranslationProvider(new HttpClient()),
                ],
                new AppSettingsStore()
            ),
            new CaptureSessionStorage(),
            new Sublingual.Infrastructure.Audio.Processing.AudioFormatNormalizer(),
            new Sublingual.Infrastructure.Audio.Processing.VoskInputVerifier(),
            new AppSettingsStore());
    }
}

public sealed class AudioDeviceItemViewModel(AudioDevice device)
{
    public string Id { get; } = device.Id;
    public string Name { get; } = device.Name;
    public bool IsDefault { get; } = device.IsDefault;
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;
}

public sealed class LanguageOptionViewModel(string name, string code)
{
    public string Name { get; } = name;
    public string Code { get; } = code;
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

public sealed partial class AudioLevelBarViewModel : ObservableObject
{
    [ObservableProperty] private double height = 10;
    [ObservableProperty] private double opacity = 0.30;
}
