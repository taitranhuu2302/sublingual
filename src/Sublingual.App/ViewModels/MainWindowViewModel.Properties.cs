using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.App.Services.Translation;
using Sublingual.Domain.Audio;
using Sublingual.Domain.Transcription;

namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
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
    [ObservableProperty] private string speechToTextModelsDirectoryPath;
    [ObservableProperty] private string sessionName = string.Empty;
    [ObservableProperty] private string sessionFolderId = string.Empty;
    [ObservableProperty] private SessionFolderOptionViewModel? selectedSessionFolder;
    [ObservableProperty] private bool isCreateSessionDialogOpen;
    [ObservableProperty] private string newSessionFolderName = string.Empty;
    [ObservableProperty] private string newSessionFolderValidationError = string.Empty;
    [ObservableProperty] private bool isRenameSessionFolderDialogOpen;
    [ObservableProperty] private string renameSessionFolderName = string.Empty;
    [ObservableProperty] private string renameSessionFolderValidationError = string.Empty;
    [ObservableProperty] private bool isDeleteSessionFolderDialogOpen;
    [ObservableProperty] private bool isMoveSessionsDialogOpen;
    [ObservableProperty] private SessionFolderOptionViewModel? moveTargetSessionFolder;
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
    [ObservableProperty] private string selectedSessionTreePath = string.Empty;
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
    public ObservableCollection<SessionFolderOptionViewModel> SessionFolders { get; }

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
    public bool HasSavedSessions => _allSavedSessions.Count > 0;
    public bool NoSavedSessions => !HasSavedSessions;
    public bool NoSearchResults => HasSavedSessions && SavedSessions.Count == 0;
    public bool CanCreateSessionFolder => string.IsNullOrWhiteSpace(NewSessionFolderValidationError)
        && !string.IsNullOrWhiteSpace(NewSessionFolderName);
    public bool CanRenameSessionFolder => string.IsNullOrWhiteSpace(RenameSessionFolderValidationError)
        && !string.IsNullOrWhiteSpace(RenameSessionFolderName)
        && SelectedSessionFolder is not null
        && !string.Equals(SelectedSessionFolder.FolderId, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase);
    public bool HasNewSessionFolderValidationError => !string.IsNullOrWhiteSpace(NewSessionFolderValidationError);
    public bool HasSelectedSessions => SavedSessions.Any(session => session.IsSelected);
    public bool HasSelectedSavedSession => SelectedSavedSession is not null;
    public bool IsGlobalSessionFolderSelected => string.Equals(SessionFolderId, CaptureSessionStorage.GlobalSessionFolderId, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(SessionFolderId);
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
}
