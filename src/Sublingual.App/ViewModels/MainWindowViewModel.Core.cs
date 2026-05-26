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
using Sublingual.Infrastructure.Audio.Processing;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AudioCaptureDebugSession _session;
    private readonly SpeechToTextModelCatalog _modelCatalog;
    private readonly SpeechToTextModelSourceCatalog _modelSourceCatalog;
    private readonly SpeechToTextDefaultModelInstaller _defaultModelInstaller;
    private readonly SpeechToTextModelImporter _modelImporter;
    private readonly CaptureSessionStorage _sessionStorage;
    private readonly AppSettingsStore _settingsStore;
    private readonly SpeechToTextRuntimeOptions _speechToTextRuntimeOptions;
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
    private bool _suspendStorageSettingsSave;
    private bool _isUpdatingSessionSelection;

    public Action? ToggleOverlayAction { get; set; }
    public Action? EnsureOverlayVisibleAction { get; set; }
    public Func<Task<string?>>? PickSessionsDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelsRootDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelDirectoryAsync { get; set; }
    public Func<Task<string?>>? PickSpeechToTextModelZipFileAsync { get; set; }

    public MainWindowViewModel(
        AudioCaptureDebugSession session,
        SpeechToTextModelCatalog modelCatalog,
        SpeechToTextModelSourceCatalog modelSourceCatalog,
        SpeechToTextDefaultModelInstaller defaultModelInstaller,
        SpeechToTextModelImporter modelImporter,
        CaptureSessionStorage sessionStorage,
        AppSettingsStore settingsStore,
        ITranscriptionService? transcriptionService = null,
        SpeechToTextRuntimeOptions? speechToTextRuntimeOptions = null,
        ITranslationService? translationService = null)
    {
        _session = session;
        _modelCatalog = modelCatalog;
        _modelSourceCatalog = modelSourceCatalog;
        _defaultModelInstaller = defaultModelInstaller;
        _modelImporter = modelImporter;
        _sessionStorage = sessionStorage;
        _settingsStore = settingsStore;
        _speechToTextRuntimeOptions = speechToTextRuntimeOptions ?? CreateSpeechToTextRuntimeOptions(settingsStore);
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
        InstallableSpeechModels = [];
        for (var i = 0; i < WaveformSampleCapacity; i++)
        {
            AudioLevelBars.Add(new AudioLevelBarViewModel());
        }

        statusMessage = "Ready.";
        outputFilePath = _outputPath;
        totalBytesText = "0 bytes";
        currentPlatform = DetectPlatform();
        captureState = _session.State.ToString();
        pipelineSummary = BuildPipelineSummary(_speechToTextRuntimeOptions.ChunkWindow);
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
        if (SelectedSpeechToTextModel is not null)
        {
            _ = PreloadSelectedSpeechToTextModelAsync(SelectedSpeechToTextModel.Name);
        }

        _ = LoadDevicesAsync();
    }

    public MainWindowViewModel()
        : this(
            CreateDesignTimeSession(),
            CreateDesignTimeModelCatalog(),
            new SpeechToTextModelSourceCatalog(),
            new SpeechToTextDefaultModelInstaller(new HttpClient(), new SpeechToTextModelImporter(CreateDesignTimeModelCatalog())),
            new SpeechToTextModelImporter(CreateDesignTimeModelCatalog()),
            new CaptureSessionStorage(new AppSettingsStore(), new SessionIndexStore(new LocalSqliteDatabase())),
            new AppSettingsStore(),
            speechToTextRuntimeOptions: CreateSpeechToTextRuntimeOptions(new AppSettingsStore()),
            translationService: new ConfigurableTranslationService(
                [
                    new TranslateServiceLocalTranslationProvider(new HttpClient()),
                    new GoogleTranslateFreeApiTranslationProvider(new HttpClient()),
                    new LibreTranslateTranslationProvider(new HttpClient()),
                ],
                new AppSettingsStore()
            ))
    {
    }

    private static SpeechToTextRuntimeOptions CreateSpeechToTextRuntimeOptions(AppSettingsStore settingsStore)
    {
        var runtimeOptions = new SpeechToTextRuntimeOptions();
        runtimeOptions.ApplyChunkPreset(settingsStore.Load().SpeechToText.RealtimeChunkPreset);
        return runtimeOptions;
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

    public void OpenSpeakingPracticeSettings()
    {
        ActiveTab = "settings";
        ActiveSettingsTab = "translation";
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
}
