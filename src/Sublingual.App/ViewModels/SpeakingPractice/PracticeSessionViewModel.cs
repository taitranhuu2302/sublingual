using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sublingual.App.Models;
using Sublingual.App.Services;
using Sublingual.Application.SpeakingPractice;
using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.App.ViewModels.SpeakingPractice;

/// <summary>
/// Drives the Speaking Practice UI. Connects to <see cref="SpeakingSessionManager"/>
/// and forwards state changes onto the Avalonia UI thread via Dispatcher.
/// </summary>
public sealed partial class PracticeSessionViewModel : ViewModelBase, IDisposable
{
    private readonly SpeakingSessionManager _sessionManager;
    private readonly AppSettingsStore _settingsStore;
    private bool _disposed;

    // ── Observable State ───────────────────────────────────────────────────────

    [ObservableProperty]
    private SpeakingSessionState _sessionState = SpeakingSessionState.Idle;

    [ObservableProperty]
    private bool _isSessionActive;

    [ObservableProperty]
    private bool _isThinking;

    [ObservableProperty]
    private bool _isSpeaking;

    [ObservableProperty]
    private string _topic = string.Empty;

    [ObservableProperty]
    private string _statusText = "Select a topic and press Start to begin.";

    public ObservableCollection<PracticeMessageViewModel> Messages { get; } = [];
    public ObservableCollection<SuggestionOption> Suggestions { get; } = [];

    // ── Constructor ────────────────────────────────────────────────────────────

    public PracticeSessionViewModel(
        SpeakingSessionManager sessionManager,
        AppSettingsStore settingsStore)
    {
        _sessionManager = sessionManager;
        _settingsStore = settingsStore;

        _sessionManager.StateChanged += OnSessionStateChanged;
        _sessionManager.MessageAdded += OnMessageAdded;
        _sessionManager.SuggestionsUpdated += OnSuggestionsUpdated;
    }

    // Design-time constructor
    public PracticeSessionViewModel() : this(
        new SpeakingSessionManager(
            new DesignTimeAiTutor(),
            new DesignTimeTts(),
            new DesignTimeMicTranscription()),
        new AppSettingsStore())
    {
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartSession()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            StatusText = "Please enter a topic first.";
            return;
        }

        var settings = _settingsStore.Load();
        Messages.Clear();
        Suggestions.Clear();

        _sessionManager.StartSession(Topic, settings.SpeakingPractice.LanguageLevel);
        IsSessionActive = true;
        StatusText = $"Listening on topic: {Topic}";
    }

    [RelayCommand]
    private void StopSession()
    {
        _sessionManager.StopSession();
        IsSessionActive = false;
        StatusText = "Session ended.";
    }

    [RelayCommand]
    private async Task ChooseSuggestionAsync(SuggestionOption suggestion)
    {
        if (!IsSessionActive || IsThinking)
        {
            return;
        }

        await _sessionManager.HandleUserTranscriptAsync(suggestion.Text);
    }

    /// <summary>Called from the View's code-behind when Vosk emits a final result.</summary>
    public async Task HandleVoskTranscriptAsync(string transcript)
    {
        if (!IsSessionActive || IsThinking || string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        await _sessionManager.HandleUserTranscriptAsync(transcript);
    }

    // ── Event Handlers (marshal to UI thread) ──────────────────────────────────

    private void OnSessionStateChanged(object? sender, SpeakingSessionState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SessionState = state;
            IsThinking = state == SpeakingSessionState.AiThinking ||
                         state == SpeakingSessionState.Transcribing;
            IsSpeaking = state == SpeakingSessionState.AiSpeaking;

            StatusText = state switch
            {
                SpeakingSessionState.Listening     => "Listening... speak now.",
                SpeakingSessionState.Transcribing  => "Processing your speech...",
                SpeakingSessionState.AiThinking    => "Tutor is thinking...",
                SpeakingSessionState.AiSpeaking    => "Tutor is speaking...",
                SpeakingSessionState.Paused        => "Session paused.",
                _                                  => StatusText,
            };
        });
    }

    private void OnMessageAdded(object? sender, PracticeMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // If user message already exists, replace it (enhancement patch).
            var existing = Messages.FirstOrDefault(m => m.Id == message.Id);
            if (existing is not null)
            {
                var idx = Messages.IndexOf(existing);
                Messages[idx] = new PracticeMessageViewModel(message);
            }
            else
            {
                Messages.Add(new PracticeMessageViewModel(message));
            }
        });
    }

    private void OnSuggestionsUpdated(object? sender, IReadOnlyList<SuggestionOption> suggestions)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Suggestions.Clear();
            foreach (var s in suggestions)
            {
                Suggestions.Add(s);
            }
        });
    }

    // ── Disposal ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionManager.StateChanged -= OnSessionStateChanged;
        _sessionManager.MessageAdded -= OnMessageAdded;
        _sessionManager.SuggestionsUpdated -= OnSuggestionsUpdated;
        _sessionManager.Dispose();
    }

    // ── Design-time stubs ──────────────────────────────────────────────────────

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
            string topic, string languageLevel,
            IReadOnlyList<PracticeMessage> history, string userText,
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

/// <summary>Flat view model wrapping a <see cref="PracticeMessage"/> for easy XAML binding.</summary>
public sealed class PracticeMessageViewModel
{
    public string Id { get; }
    public bool IsUser { get; }
    public string Text { get; }
    public string? EnhancementAdvice { get; }
    public bool HasEnhancement => !string.IsNullOrWhiteSpace(EnhancementAdvice);
    public DateTimeOffset Timestamp { get; }

    public PracticeMessageViewModel(PracticeMessage message)
    {
        Id = message.Id;
        IsUser = message.Sender == MessageSender.User;
        Text = message.Text;
        EnhancementAdvice = message.EnhancementAdvice;
        Timestamp = message.Timestamp;
    }
}
