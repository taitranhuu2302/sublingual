using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
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
    private bool _suspendStorageSettingsSave;

    public Action? ToggleOverlayAction { get; set; }
    public Action? EnsureOverlayVisibleAction { get; set; }
    public Func<Task<string?>>? PickSessionsDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelsRootDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelZipFileAsync { get; set; }

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
        SessionFolders = [];
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
        speechToTextModelsDirectoryPath = _modelCatalog.GetManagedModelsRoot();
        speechToTextStatus = _transcriptionService is VoskTranscriptionService vosk
            ? $"Active model: {vosk.CurrentModelName}"
            : "Speech-to-text provider ready.";
        sessionFolderId = _sessionStorage.GetPreferredFolderId();

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
            CreateDesignTimeModelCatalog(),
            new SpeechToTextModelImporter(CreateDesignTimeModelCatalog()),
            new CaptureSessionStorage(new AppSettingsStore()),
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

    public void Dispose()
    {
        if (_disposed) return;
        PropertyChanged -= OnPropertyChanged;
        _session.ChunkObserved -= OnChunkObserved;
        _session.TranscriptPreviewUpdated -= OnTranscriptPreviewUpdated;
        _session.Dispose();
        _disposed = true;
    }
}
