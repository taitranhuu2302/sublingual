using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.Application.SpeakingPractice;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.App.ViewModels.SpeakingPractice;

public sealed partial class PracticeSessionViewModel : ViewModelBase, IDisposable
{
    private const string DefaultInstructions = "Have a warm daily English conversation with the user. Greet them, ask about their day, work, feelings, family life, or everyday concerns, and offer gentle advice when it feels natural.";

    private readonly SpeakingSessionManager _sessionManager;
    private readonly AppSettingsStore _settingsStore;
    private readonly SpeakingPracticeRoomStore _roomStore;
    private readonly IMicrophoneTranscriptionService _micTranscription;
    private readonly List<SpeakingPracticeRoomRecord> _allRooms = [];
    private readonly HashSet<string> _spokenMessageIds = [];
    private readonly List<string> _recordingSegments = [];
    private readonly List<string> _pendingDeleteRoomIds = [];
    private CancellationTokenSource? _recordingCts;
    private bool _disposed;
    public Action? OpenSettingsAction { get; set; }

    [ObservableProperty] private string _activePage = "list";
    [ObservableProperty] private bool _isCreateRoomDialogOpen;
    [ObservableProperty] private bool _isEditRoomDialogOpen;
    [ObservableProperty] private bool _isDeleteRoomsDialogOpen;
    [ObservableProperty] private string _deleteRoomsDialogMessage = string.Empty;
    [ObservableProperty] private string _roomSearchText = string.Empty;
    [ObservableProperty] private string _newRoomTitle = string.Empty;
    [ObservableProperty] private string _newRoomInstructions = string.Empty;
    [ObservableProperty] private string _editRoomTitle = string.Empty;
    [ObservableProperty] private string _editRoomInstructions = string.Empty;
    [ObservableProperty] private string _newRoomValidationError = string.Empty;
    [ObservableProperty] private string _editRoomValidationError = string.Empty;
    [ObservableProperty] private SpeakingPracticeRoomItemViewModel? _selectedRoom;
    [ObservableProperty] private string _roomTitle = string.Empty;
    [ObservableProperty] private string _roomInstructions = string.Empty;
    [ObservableProperty] private string _typedMessage = string.Empty;
    [ObservableProperty] private string _recordingTranscriptPreview = string.Empty;
    [ObservableProperty] private SpeakingSessionState _sessionState = SpeakingSessionState.Idle;
    [ObservableProperty] private bool _isThinking;
    [ObservableProperty] private bool _isSpeaking;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isRoomActionBusy;
    [ObservableProperty] private bool _isAiConfigured = true;
    [ObservableProperty] private string _aiConfigurationError = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<SpeakingPracticeRoomItemViewModel> Rooms { get; } = [];
    public ObservableCollection<PracticeMessageViewModel> Messages { get; } = [];
    public ObservableCollection<SuggestionOption> Suggestions { get; } = [];

    public bool IsListPageActive => string.Equals(ActivePage, "list", StringComparison.OrdinalIgnoreCase);
    public bool IsDetailPageActive => string.Equals(ActivePage, "detail", StringComparison.OrdinalIgnoreCase);
    public bool HasRooms => Rooms.Count > 0;
    public bool NoRooms => !HasRooms;
    public bool HasAnyRooms => _allRooms.Count > 0;
    public bool NoSearchResults => HasAnyRooms && Rooms.Count == 0;
    public bool HasSelectedRooms => Rooms.Any(room => room.IsSelected);
    public bool HasSelectedRoom => SelectedRoom is not null;
    public bool CanCreateRoom => !IsRoomActionBusy && string.IsNullOrWhiteSpace(NewRoomValidationError);
    public bool HasCreateRoomValidationError => !string.IsNullOrWhiteSpace(NewRoomValidationError);
    public bool CanSaveRoomEdits => !IsRoomActionBusy && HasSelectedRoom && string.IsNullOrWhiteSpace(EditRoomValidationError);
    public bool HasEditRoomValidationError => !string.IsNullOrWhiteSpace(EditRoomValidationError);
    public bool CanSendTypedMessage => IsAiConfigured && !IsRoomActionBusy && HasSelectedRoom && !IsThinking && !IsRecording && !string.IsNullOrWhiteSpace(TypedMessage);
    public bool CanChooseSuggestion => IsAiConfigured && !IsRoomActionBusy && HasSelectedRoom && !IsThinking && !IsRecording;
    public bool CanStartSpeaking => IsAiConfigured && !IsRoomActionBusy && HasSelectedRoom && !IsThinking && !IsRecording;
    public bool CanStopSpeaking => IsRecording;
    public bool HasRecordingTranscriptPreview => !string.IsNullOrWhiteSpace(RecordingTranscriptPreview);
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);
    public bool ShowOpenSettingsForAi => !IsAiConfigured && HasSelectedRoom;
    public string AiActionHint => IsAiConfigured ? "" : "Configure AI provider key/model in Settings.";
    public string ActiveAiProviderLabel { get; private set; } = "Groq";
    public string ActiveAiModelLabel { get; private set; } = "qwen/qwen3-32b";
    public string ActiveAiRuntimeLabel => $"{ActiveAiProviderLabel} • {ActiveAiModelLabel}";

    public PracticeSessionViewModel(
        SpeakingSessionManager sessionManager,
        AppSettingsStore settingsStore,
        SpeakingPracticeRoomStore roomStore,
        IMicrophoneTranscriptionService micTranscription)
    {
        _sessionManager = sessionManager;
        _settingsStore = settingsStore;
        _roomStore = roomStore;
        _micTranscription = micTranscription;

        _sessionManager.StateChanged += OnSessionStateChanged;
        _sessionManager.MessageAdded += OnMessageAdded;
        _sessionManager.SuggestionsUpdated += OnSuggestionsUpdated;
        _micTranscription.FinalTranscriptReady += OnFinalTranscriptReady;

        LoadRooms();
    }

    public PracticeSessionViewModel() : this(
        new SpeakingSessionManager(
            new DesignTimeAiTutor(),
            new DesignTimeTts()),
        new AppSettingsStore(),
        new SpeakingPracticeRoomStore(),
        new DesignTimeMicTranscription())
    {
    }

    [RelayCommand]
    private void OpenCreateRoomDialog()
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        NewRoomTitle = string.Empty;
        NewRoomInstructions = string.Empty;
        NewRoomValidationError = string.Empty;
        IsCreateRoomDialogOpen = true;
        CreateRoomCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CloseCreateRoomDialog()
    {
        IsCreateRoomDialogOpen = false;
    }

    [RelayCommand]
    private void OpenEditRoomDialog()
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        if (SelectedRoom is null)
        {
            StatusText = "Open a room first.";
            return;
        }

        EditRoomTitle = RoomTitle;
        EditRoomInstructions = string.Equals(RoomInstructions, DefaultInstructions, StringComparison.Ordinal)
            ? string.Empty
            : RoomInstructions;
        EditRoomValidationError = string.Empty;
        IsEditRoomDialogOpen = true;
        SaveRoomEditsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CloseEditRoomDialog()
    {
        IsEditRoomDialogOpen = false;
    }

    [RelayCommand]
    private void CloseDeleteRoomsDialog()
    {
        IsDeleteRoomsDialogOpen = false;
        DeleteRoomsDialogMessage = string.Empty;
        _pendingDeleteRoomIds.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanCreateRoom))]
    private void CreateRoom()
    {
        IsRoomActionBusy = true;
        try
        {
            var room = _roomStore.CreateRoom(NewRoomTitle, NewRoomInstructions);
            IsCreateRoomDialogOpen = false;
            NewRoomTitle = string.Empty;
            NewRoomInstructions = string.Empty;
            LoadRooms(room.Id);
            OpenRoomById(room.Id);
            StatusText = "Practice room created.";
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand]
    private void DeleteSelectedRooms()
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        var selectedIds = Rooms.Where(room => room.IsSelected).Select(room => room.Id).ToList();
        if (selectedIds.Count == 0)
        {
            StatusText = "Select at least one room to delete.";
            return;
        }

        _pendingDeleteRoomIds.Clear();
        _pendingDeleteRoomIds.AddRange(selectedIds);
        DeleteRoomsDialogMessage = selectedIds.Count == 1
            ? "Delete the selected room? This will also remove its conversation history."
            : $"Delete {selectedIds.Count} selected rooms? This will also remove their conversation history.";
        IsDeleteRoomsDialogOpen = true;
    }

    [RelayCommand]
    private void OpenDeleteRoomDialog(SpeakingPracticeRoomItemViewModel? room)
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        if (room is null)
        {
            return;
        }

        _pendingDeleteRoomIds.Clear();
        _pendingDeleteRoomIds.Add(room.Id);
        DeleteRoomsDialogMessage = $"Delete room '{room.Name}'? This will also remove its conversation history.";
        IsDeleteRoomsDialogOpen = true;
    }

    [RelayCommand]
    private void DuplicateRoom(SpeakingPracticeRoomItemViewModel? room)
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        if (room is null)
        {
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            var source = _roomStore.GetRoom(room.Id);
            if (source is null)
            {
                StatusText = "Room no longer exists.";
                LoadRooms();
                return;
            }

            var duplicate = _roomStore.CreateRoom($"{source.Name} Copy", source.Instructions);
            LoadRooms(duplicate.Id);
            OpenRoomById(duplicate.Id);
            StatusText = "Room duplicated.";
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand]
    private void DuplicateCurrentRoom()
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        DuplicateRoom(SelectedRoom);
    }

    [RelayCommand]
    private void ConfirmDeleteRooms()
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        if (_pendingDeleteRoomIds.Count == 0)
        {
            CloseDeleteRoomsDialog();
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            var pendingIds = _pendingDeleteRoomIds.ToList();
            var deletedCount = _roomStore.DeleteRooms(pendingIds);
            if (SelectedRoom is not null && pendingIds.Contains(SelectedRoom.Id, StringComparer.Ordinal))
            {
                LeaveRoomInternal();
            }

            CloseDeleteRoomsDialog();
            LoadRooms();
            StatusText = deletedCount == 1 ? "Deleted 1 room." : $"Deleted {deletedCount} rooms.";
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveRoomEdits))]
    private void SaveRoomEdits()
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        if (SelectedRoom is null)
        {
            IsEditRoomDialogOpen = false;
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            var updated = _roomStore.UpdateRoom(SelectedRoom.Id, EditRoomTitle, EditRoomInstructions);
            if (updated is null)
            {
                IsEditRoomDialogOpen = false;
                StatusText = "Room no longer exists.";
                LoadRooms();
                return;
            }

            RoomTitle = updated.Name;
            RoomInstructions = string.IsNullOrWhiteSpace(updated.Instructions) ? DefaultInstructions : updated.Instructions;
            _sessionManager.LoadConversation(RoomInstructions, _settingsStore.Load().SpeakingPractice.LanguageLevel, _sessionManager.History);
            IsEditRoomDialogOpen = false;
            LoadRooms(updated.Id);
            StatusText = "Room updated.";
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand]
    private void OpenRoom(SpeakingPracticeRoomItemViewModel? room)
    {
        if (IsRoomActionBusy)
        {
            return;
        }

        if (room is null)
        {
            return;
        }

        OpenRoomById(room.Id);
    }

    [RelayCommand]
    private void BackToRoomList()
    {
        LeaveRoomInternal();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private void OpenSpeakingSettings()
    {
        OpenSettingsAction?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanSendTypedMessage))]
    private async Task SendTypedMessageAsync()
    {
        if (!EnsureAiConfigurationReady())
        {
            return;
        }

        var message = TypedMessage.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            TypedMessage = string.Empty;
            await SubmitUserMessageAsync(message, isSpoken: false);
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChooseSuggestion))]
    private async Task ChooseSuggestionAsync(SuggestionOption? suggestion)
    {
        if (!EnsureAiConfigurationReady())
        {
            return;
        }

        if (suggestion is null)
        {
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            await SubmitUserMessageAsync(suggestion.Text, isSpoken: false);
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartSpeaking))]
    private async Task StartSpeakingAsync()
    {
        if (!EnsureAiConfigurationReady())
        {
            return;
        }

        if (SelectedRoom is null)
        {
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            _sessionManager.CancelActiveResponse();
            _recordingSegments.Clear();
            RecordingTranscriptPreview = string.Empty;
            _recordingCts?.Dispose();
            _recordingCts = new CancellationTokenSource();

            try
            {
                await _micTranscription.StartAsync(_recordingCts.Token);
                IsRecording = true;
                StatusText = "Recording... press Stop when you finish speaking.";
            }
            catch (Exception ex)
            {
                _recordingCts.Dispose();
                _recordingCts = null;
                IsRecording = false;
                StatusText = $"Could not start microphone: {ex.Message}";
            }
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopSpeaking))]
    private async Task StopSpeakingAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        IsRoomActionBusy = true;
        try
        {
            try
            {
                await _micTranscription.StopAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Could not stop microphone: {ex.Message}";
                return;
            }
            finally
            {
                _recordingCts?.Cancel();
                _recordingCts?.Dispose();
                _recordingCts = null;
                IsRecording = false;
            }

            var transcript = string.Join(" ", _recordingSegments).Trim();
            _recordingSegments.Clear();
            RecordingTranscriptPreview = string.Empty;

            if (string.IsNullOrWhiteSpace(transcript))
            {
                StatusText = "No speech was captured. Try again.";
                return;
            }

            _sessionManager.MarkTranscribing();
            await SubmitUserMessageAsync(transcript, isSpoken: true);
        }
        finally
        {
            IsRoomActionBusy = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _recordingCts?.Cancel();
        _recordingCts?.Dispose();
        _ = _micTranscription.StopAsync();

        _sessionManager.StateChanged -= OnSessionStateChanged;
        _sessionManager.MessageAdded -= OnMessageAdded;
        _sessionManager.SuggestionsUpdated -= OnSuggestionsUpdated;
        _micTranscription.FinalTranscriptReady -= OnFinalTranscriptReady;
        _sessionManager.Dispose();
        (_micTranscription as IDisposable)?.Dispose();
    }

    partial void OnNewRoomInstructionsChanged(string value)
    {
        ValidateCreateRoomForm();
    }

    partial void OnNewRoomTitleChanged(string value)
    {
        ValidateCreateRoomForm();
    }

    partial void OnEditRoomTitleChanged(string value)
    {
        ValidateEditRoomForm();
    }

    partial void OnEditRoomInstructionsChanged(string value)
    {
        ValidateEditRoomForm();
    }

    partial void OnRoomSearchTextChanged(string value)
    {
        ApplyRoomFilter();
    }

    private void ValidateCreateRoomForm()
    {
        if (NewRoomTitle.Length > 80)
        {
            NewRoomValidationError = "Room title is too long. Keep it under 80 characters.";
        }
        else if (NewRoomInstructions.Length > 1000)
        {
            NewRoomValidationError = "Instructions are too long. Keep them under 1000 characters.";
        }
        else
        {
            NewRoomValidationError = string.Empty;
        }

        OnPropertyChanged(nameof(HasCreateRoomValidationError));
        OnPropertyChanged(nameof(CanCreateRoom));
        CreateRoomCommand.NotifyCanExecuteChanged();
    }

    private void ValidateEditRoomForm()
    {
        if (EditRoomTitle.Length > 80)
        {
            EditRoomValidationError = "Room title is too long. Keep it under 80 characters.";
        }
        else if (EditRoomInstructions.Length > 1000)
        {
            EditRoomValidationError = "Instructions are too long. Keep them under 1000 characters.";
        }
        else
        {
            EditRoomValidationError = string.Empty;
        }

        OnPropertyChanged(nameof(HasEditRoomValidationError));
        OnPropertyChanged(nameof(CanSaveRoomEdits));
        SaveRoomEditsCommand.NotifyCanExecuteChanged();
    }

    partial void OnTypedMessageChanged(string value)
    {
        OnPropertyChanged(nameof(CanSendTypedMessage));
        SendTypedMessageCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusText));
    }

    partial void OnIsAiConfiguredChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowOpenSettingsForAi));
        OnPropertyChanged(nameof(AiActionHint));
        OnPropertyChanged(nameof(CanSendTypedMessage));
        OnPropertyChanged(nameof(CanChooseSuggestion));
        OnPropertyChanged(nameof(CanStartSpeaking));
        SendTypedMessageCommand.NotifyCanExecuteChanged();
        ChooseSuggestionCommand.NotifyCanExecuteChanged();
        StartSpeakingCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRoomChanged(SpeakingPracticeRoomItemViewModel? value)
    {
        OnPropertyChanged(nameof(ShowOpenSettingsForAi));
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartSpeaking));
        OnPropertyChanged(nameof(CanStopSpeaking));
        StartSpeakingCommand.NotifyCanExecuteChanged();
        StopSpeakingCommand.NotifyCanExecuteChanged();
        SendTypedMessageCommand.NotifyCanExecuteChanged();
        ChooseSuggestionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsRoomActionBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreateRoom));
        OnPropertyChanged(nameof(CanSaveRoomEdits));
        OnPropertyChanged(nameof(CanSendTypedMessage));
        OnPropertyChanged(nameof(CanChooseSuggestion));
        OnPropertyChanged(nameof(CanStartSpeaking));

        CreateRoomCommand.NotifyCanExecuteChanged();
        SaveRoomEditsCommand.NotifyCanExecuteChanged();
        SendTypedMessageCommand.NotifyCanExecuteChanged();
        ChooseSuggestionCommand.NotifyCanExecuteChanged();
        StartSpeakingCommand.NotifyCanExecuteChanged();
        StopSpeakingCommand.NotifyCanExecuteChanged();
    }

    partial void OnActivePageChanged(string value)
    {
        OnPropertyChanged(nameof(IsListPageActive));
        OnPropertyChanged(nameof(IsDetailPageActive));
    }

    private void OnSessionStateChanged(object? sender, SpeakingSessionState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SessionState = state;
            IsThinking = state == SpeakingSessionState.AiThinking || state == SpeakingSessionState.Transcribing;
            IsSpeaking = state == SpeakingSessionState.AiSpeaking;

            StatusText = state switch
            {
                SpeakingSessionState.Listening => string.Empty,
                SpeakingSessionState.Transcribing => "Processing your speech...",
                SpeakingSessionState.AiThinking => "Tutor is thinking...",
                SpeakingSessionState.AiSpeaking => "Tutor is speaking...",
                SpeakingSessionState.Idle => HasSelectedRoom ? "Conversation paused." : StatusText,
                _ => StatusText,
            };

            SendTypedMessageCommand.NotifyCanExecuteChanged();
            ChooseSuggestionCommand.NotifyCanExecuteChanged();
            StartSpeakingCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnMessageAdded(object? sender, PracticeMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Messages.FirstOrDefault(item => item.Id == message.Id);
            var viewModel = new PracticeMessageViewModel(message, _spokenMessageIds.Contains(message.Id));
            if (existing is not null)
            {
                var index = Messages.IndexOf(existing);
                Messages[index] = viewModel;
            }
            else
            {
                Messages.Add(viewModel);
            }

            PersistCurrentRoomMessages();
        });
    }

    private void OnSuggestionsUpdated(object? sender, IReadOnlyList<SuggestionOption> suggestions)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Suggestions.Clear();
            foreach (var suggestion in suggestions)
            {
                Suggestions.Add(suggestion);
            }

            ChooseSuggestionCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnFinalTranscriptReady(object? sender, string transcript)
    {
        if (!IsRecording || string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _recordingSegments.Add(transcript.Trim());
            RecordingTranscriptPreview = string.Join(" ", _recordingSegments);
            OnPropertyChanged(nameof(HasRecordingTranscriptPreview));
        });
    }

    private async Task SubmitUserMessageAsync(string text, bool isSpoken)
    {
        if (SelectedRoom is null)
        {
            return;
        }

        RefreshAiConfigurationStatus();
        if (IsAiConfigured)
        {
            StatusText = $"Sending via {ActiveAiRuntimeLabel}...";
        }

        var beforeIds = _sessionManager.History.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
        await _sessionManager.HandleUserTranscriptAsync(text);

        if (isSpoken)
        {
            foreach (var message in _sessionManager.History.Where(message => message.Sender == MessageSender.User && !beforeIds.Contains(message.Id)))
            {
                _spokenMessageIds.Add(message.Id);
            }
        }

        PersistCurrentRoomMessages();
    }

    private void LoadRooms(string? selectedRoomId = null)
    {
        var currentSelection = selectedRoomId ?? SelectedRoom?.Id;
        _allRooms.Clear();
        _allRooms.AddRange(_roomStore.GetRooms());

        ApplyRoomFilter(currentSelection);
    }

    private void ApplyRoomFilter(string? selectedRoomId = null)
    {
        var currentSelection = selectedRoomId ?? SelectedRoom?.Id;
        var search = RoomSearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allRooms
            : _allRooms.Where(room =>
                room.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                room.Instructions.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Rooms.Clear();
        foreach (var room in filtered)
        {
            var item = new SpeakingPracticeRoomItemViewModel(
                room.Id,
                room.Name,
                room.Instructions,
                room.CreatedAt,
                room.UpdatedAt,
                room.Messages.Count == 0 ? string.Empty : room.Messages.OrderByDescending(message => message.Timestamp).First().Text,
                room.Messages.Count);
            item.PropertyChanged += OnRoomItemPropertyChanged;
            Rooms.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(currentSelection))
        {
            SelectedRoom = Rooms.FirstOrDefault(room => string.Equals(room.Id, currentSelection, StringComparison.Ordinal));
        }

        OnPropertyChanged(nameof(HasRooms));
        OnPropertyChanged(nameof(NoRooms));
        OnPropertyChanged(nameof(HasAnyRooms));
        OnPropertyChanged(nameof(NoSearchResults));
        OnPropertyChanged(nameof(HasSelectedRooms));
    }

    private void OpenRoomById(string roomId)
    {
        var room = _roomStore.GetRoom(roomId);
        if (room is null)
        {
            StatusText = "Room no longer exists.";
            LoadRooms();
            return;
        }

        _recordingSegments.Clear();
        RecordingTranscriptPreview = string.Empty;
        Suggestions.Clear();
        Messages.Clear();
        _spokenMessageIds.Clear();

        SelectedRoom = Rooms.FirstOrDefault(item => string.Equals(item.Id, room.Id, StringComparison.Ordinal))
            ?? new SpeakingPracticeRoomItemViewModel(
                room.Id,
                room.Name,
                room.Instructions,
                room.CreatedAt,
                room.UpdatedAt,
                room.Messages.Count == 0 ? string.Empty : room.Messages.OrderByDescending(message => message.Timestamp).First().Text,
                room.Messages.Count);
        RoomTitle = room.Name;
        RoomInstructions = string.IsNullOrWhiteSpace(room.Instructions) ? DefaultInstructions : room.Instructions;
        RefreshAiConfigurationStatus();

        foreach (var messageRecord in room.Messages.OrderBy(message => message.Timestamp))
        {
            var message = new PracticeMessage(
                messageRecord.Id,
                Enum.TryParse<MessageSender>(messageRecord.Sender, true, out var sender) ? sender : MessageSender.User,
                messageRecord.Text,
                messageRecord.EnhancementAdvice,
                messageRecord.Timestamp);
            if (messageRecord.IsSpoken)
            {
                _spokenMessageIds.Add(message.Id);
            }

            Messages.Add(new PracticeMessageViewModel(message, messageRecord.IsSpoken));
        }

        var settings = _settingsStore.Load();
        _sessionManager.LoadConversation(RoomInstructions, settings.SpeakingPractice.LanguageLevel, SpeakingPracticeRoomStore.ToDomainMessages(room));

        ActivePage = "detail";
        StatusText = IsAiConfigured ? string.Empty : AiConfigurationError;
        PersistCurrentRoomMessages();
    }

    private void LeaveRoomInternal()
    {
        _recordingSegments.Clear();
        RecordingTranscriptPreview = string.Empty;
        Suggestions.Clear();
        Messages.Clear();
        _spokenMessageIds.Clear();
        TypedMessage = string.Empty;
        SelectedRoom = null;
        RoomTitle = string.Empty;
        RoomInstructions = string.Empty;
        AiConfigurationError = string.Empty;
        IsAiConfigured = true;
        ActivePage = "list";
        _sessionManager.StopSession();
    }

    private void RefreshAiConfigurationStatus()
    {
        var settings = _settingsStore.Load().SpeakingPractice;
        ActiveAiProviderLabel = string.Equals(settings.AiProvider, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase)
            ? SpeakingPracticeProviders.Gemini
            : SpeakingPracticeProviders.Groq;
        ActiveAiModelLabel = string.Equals(ActiveAiProviderLabel, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase)
            ? (string.IsNullOrWhiteSpace(settings.GeminiModel) ? "(missing)" : settings.GeminiModel)
            : (string.IsNullOrWhiteSpace(settings.GroqModel) ? "(missing)" : settings.GroqModel);
        OnPropertyChanged(nameof(ActiveAiProviderLabel));
        OnPropertyChanged(nameof(ActiveAiModelLabel));
        OnPropertyChanged(nameof(ActiveAiRuntimeLabel));

        if (string.Equals(settings.AiProvider, SpeakingPracticeProviders.Gemini, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                IsAiConfigured = false;
                AiConfigurationError = "Gemini API key is empty. Add it in Settings before practicing.";
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.GeminiModel))
            {
                IsAiConfigured = false;
                AiConfigurationError = "Gemini model is empty. Set a model in Settings before practicing.";
                return;
            }

            IsAiConfigured = true;
            AiConfigurationError = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.GroqApiKey))
        {
            IsAiConfigured = false;
            AiConfigurationError = "Groq API key is empty. Add it in Settings before practicing.";
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.GroqModel))
        {
            IsAiConfigured = false;
            AiConfigurationError = "Groq model is empty. Set a model in Settings before practicing.";
            return;
        }

        IsAiConfigured = true;
        AiConfigurationError = string.Empty;
    }

    private bool EnsureAiConfigurationReady()
    {
        RefreshAiConfigurationStatus();
        if (IsAiConfigured)
        {
            return true;
        }

        StatusText = AiConfigurationError;
        return false;
    }

    private void PersistCurrentRoomMessages()
    {
        if (SelectedRoom is null)
        {
            return;
        }

        _roomStore.ReplaceMessages(
            SelectedRoom.Id,
            _sessionManager.History,
            message => _spokenMessageIds.Contains(message.Id));

        var target = Rooms.FirstOrDefault(room => string.Equals(room.Id, SelectedRoom.Id, StringComparison.Ordinal));
        if (target is not null)
        {
            target.MessageCount = _sessionManager.History.Count;
            target.UpdatedAt = _sessionManager.History.Count == 0
                ? target.UpdatedAt
                : _sessionManager.History.Max(message => message.Timestamp);
            target.LastMessagePreview = _sessionManager.History.Count == 0
                ? string.Empty
                : _sessionManager.History.Last().Text;
        }
    }

    private void OnRoomItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpeakingPracticeRoomItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(HasSelectedRooms));
        }
    }

    private sealed class DesignTimeMicTranscription : IMicrophoneTranscriptionService
    {
#pragma warning disable CS0067
        public event EventHandler<string>? FinalTranscriptReady;
#pragma warning restore CS0067
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void SetMuted(bool muted) { }
    }

    private sealed class DesignTimeAiTutor : IAiTutorService
    {
        public Task<TutorResponse?> GetResponseAsync(
            string instructions,
            string languageLevel,
            IReadOnlyList<PracticeMessage> history,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TutorResponse?>(null);
    }

    private sealed class DesignTimeTts : ITtsService
    {
        public bool IsSpeaking => false;
        public Task SpeakAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void StopSpeaking() { }
    }
}

public sealed partial class SpeakingPracticeRoomItemViewModel : ObservableObject
{
    public string Id { get; }
    public string Name { get; }
    public string InstructionsPreview { get; }
    public DateTimeOffset CreatedAt { get; }
    [ObservableProperty] private DateTimeOffset updatedAt;
    [ObservableProperty] private string lastMessagePreview;

    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private int messageCount;

    public string CreatedAtText => CreatedAt.LocalDateTime.ToString("g");
    public string UpdatedAtText => UpdatedAt.LocalDateTime.ToString("g");
    public bool HasLastMessagePreview => !string.IsNullOrWhiteSpace(LastMessagePreview);

    public SpeakingPracticeRoomItemViewModel(
        string id,
        string name,
        string instructions,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string lastMessagePreview,
        int messageCount)
    {
        Id = id;
        Name = name;
        InstructionsPreview = string.IsNullOrWhiteSpace(instructions)
            ? "Daily conversation"
            : instructions;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        LastMessagePreview = lastMessagePreview;
        MessageCount = messageCount;
    }

    partial void OnUpdatedAtChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(UpdatedAtText));
    }

    partial void OnLastMessagePreviewChanged(string value)
    {
        OnPropertyChanged(nameof(HasLastMessagePreview));
    }
}

public sealed class PracticeMessageViewModel
{
    public string Id { get; }
    public bool IsUser { get; }
    public string Text { get; }
    public string SingleLineText => Text.Replace("\r", " ").Replace("\n", " ");
    public string? EnhancementAdvice { get; }
    public bool HasEnhancement => !string.IsNullOrWhiteSpace(EnhancementAdvice);
    public DateTimeOffset Timestamp { get; }
    public bool IsSpoken { get; }

    public PracticeMessageViewModel(PracticeMessage message, bool isSpoken)
    {
        Id = message.Id;
        IsUser = message.Sender == MessageSender.User;
        Text = message.Text;
        EnhancementAdvice = message.EnhancementAdvice;
        Timestamp = message.Timestamp;
        IsSpoken = isSpoken;
    }
}
