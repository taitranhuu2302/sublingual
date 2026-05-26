using Sublingual.Domain.SpeakingPractice;
using Microsoft.Extensions.Logging;

namespace Sublingual.Application.SpeakingPractice;

/// <summary>
/// Orchestrates the AI speaking practice loop.
/// Owns the conversation history, AI/TTS lifecycle, and session state updates.
/// </summary>
public sealed class SpeakingSessionManager : IDisposable
{
    private readonly IAiTutorService _aiTutor;
    private readonly ITtsService _tts;
    private readonly ILogger? _logger;

    private readonly List<PracticeMessage> _history = [];
    private CancellationTokenSource? _thinkingCts;
    private SpeakingSessionState _state = SpeakingSessionState.Idle;
    private bool _disposed;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires whenever the session state transitions.</summary>
    public event EventHandler<SpeakingSessionState>? StateChanged;

    /// <summary>Fires when a new message (user or AI) is ready to display.</summary>
    public event EventHandler<PracticeMessage>? MessageAdded;

    /// <summary>Fires when fresh suggestion chips are available.</summary>
    public event EventHandler<IReadOnlyList<SuggestionOption>>? SuggestionsUpdated;

    // ── Public API ─────────────────────────────────────────────────────────────

    public string Instructions { get; private set; } = string.Empty;
    public string LanguageLevel { get; private set; } = "Intermediate";
    public SpeakingSessionState State => _state;
    public IReadOnlyList<PracticeMessage> History => _history;

    public SpeakingSessionManager(
        IAiTutorService aiTutor,
        ITtsService tts,
        ILogger<SpeakingSessionManager>? logger = null)
    {
        _aiTutor = aiTutor;
        _tts = tts;
        _logger = logger;
    }

    /// <summary>Loads a room conversation and transitions to Listening.</summary>
    public void LoadConversation(
        string instructions,
        string languageLevel,
        IEnumerable<PracticeMessage>? history = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelPendingThinking();
        _tts.StopSpeaking();
        _history.Clear();
        if (history is not null)
        {
            _history.AddRange(history);
        }

        Instructions = instructions;
        LanguageLevel = languageLevel;
        _logger?.LogInformation("Speaking session loaded. Level={Level} HistoryCount={HistoryCount}", languageLevel, _history.Count);
        SuggestionsUpdated?.Invoke(this, []);
        TransitionTo(SpeakingSessionState.Listening);
    }

    /// <summary>
    /// Called by the ViewModel when Vosk finalises a transcript.
    /// Adds the user's message and kicks off the AI → TTS pipeline.
    /// </summary>
    public async Task HandleUserTranscriptAsync(string transcript, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        _logger?.LogInformation("User transcript received. Len={Len}", transcript.Length);

        // Stop any prior TTS before reacting.
        _tts.StopSpeaking();
        CancelPendingThinking();

        // Publish user message.
        var userMessage = new PracticeMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: MessageSender.User,
            Text: transcript.Trim(),
            EnhancementAdvice: null,
            Timestamp: DateTimeOffset.Now
        );
        AddMessage(userMessage);

        TransitionTo(SpeakingSessionState.AiThinking);

        _thinkingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _thinkingCts.Token;

        TutorResponse? tutorResponse = null;
        try
        {
            tutorResponse = await _aiTutor.GetResponseAsync(
                Instructions,
                LanguageLevel,
                _history,
                linkedToken
            );
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AI request cancelled");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AI request failed");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        if (tutorResponse is null)
        {
            _logger?.LogWarning("AI returned null response");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        if (string.IsNullOrWhiteSpace(tutorResponse.TutorReply))
        {
            _logger?.LogWarning("AI returned empty reply");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        // (Removed) correction/scoring patching for user message.

        // Publish AI reply message.
        var aiMessage = new PracticeMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: MessageSender.Ai,
            Text: tutorResponse.TutorReply,
            EnhancementAdvice: null,
            Timestamp: DateTimeOffset.Now,
            Suggestions: tutorResponse.Suggestions
        );
        AddMessage(aiMessage);

        // Publish suggestions.
        SuggestionsUpdated?.Invoke(this, tutorResponse.Suggestions);

        TransitionTo(SpeakingSessionState.AiSpeaking);
        try
        {
            await _tts.SpeakAsync(tutorResponse.TutorReply, linkedToken);
        }
        catch (OperationCanceledException)
        {
            // User skipped or started speaking.
        }

        if (!linkedToken.IsCancellationRequested)
        {
            TransitionTo(SpeakingSessionState.Listening);
        }
    }

    /// <summary>
    /// Starts the conversation with an AI opening message when the room has no messages yet.
    /// Safe to call multiple times; it will no-op once history is non-empty.
    /// </summary>
    public async Task HandleTutorKickoffAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_history.Count != 0)
        {
            return;
        }

        _tts.StopSpeaking();
        CancelPendingThinking();

        TransitionTo(SpeakingSessionState.AiThinking);

        _thinkingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _thinkingCts.Token;

        TutorResponse? tutorResponse = null;
        try
        {
            tutorResponse = await _aiTutor.GetResponseAsync(
                Instructions,
                LanguageLevel,
                _history,
                linkedToken
            );
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("AI kickoff cancelled");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AI kickoff failed");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        if (tutorResponse is null)
        {
            _logger?.LogWarning("AI kickoff returned null response");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        if (string.IsNullOrWhiteSpace(tutorResponse.TutorReply))
        {
            _logger?.LogWarning("AI kickoff returned empty reply");
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        var aiMessage = new PracticeMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: MessageSender.Ai,
            Text: tutorResponse.TutorReply,
            EnhancementAdvice: null,
            Timestamp: DateTimeOffset.Now,
            Suggestions: tutorResponse.Suggestions
        );
        AddMessage(aiMessage);

        SuggestionsUpdated?.Invoke(this, tutorResponse.Suggestions);

        TransitionTo(SpeakingSessionState.AiSpeaking);
        try
        {
            await _tts.SpeakAsync(tutorResponse.TutorReply, linkedToken);
        }
        catch (OperationCanceledException)
        {
            // User skipped or started speaking.
        }

        if (!linkedToken.IsCancellationRequested)
        {
            TransitionTo(SpeakingSessionState.Listening);
        }
    }

    public void MarkTranscribing()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TransitionTo(SpeakingSessionState.Transcribing);
    }

    public void CancelActiveResponse()
    {
        CancelPendingThinking();
        _tts.StopSpeaking();
        TransitionTo(SpeakingSessionState.Listening);
    }

    public async Task SpeakTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CancelActiveResponse();
        TransitionTo(SpeakingSessionState.AiSpeaking);

        try
        {
            await _tts.SpeakAsync(text, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // User stopped or playback was interrupted.
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            TransitionTo(SpeakingSessionState.Listening);
        }
    }

    public async Task ReplayLastAiResponseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var lastAiMessage = _history.LastOrDefault(message => message.Sender == MessageSender.Ai);
        if (lastAiMessage is null || string.IsNullOrWhiteSpace(lastAiMessage.Text))
        {
            return;
        }

        await SpeakTextAsync(lastAiMessage.Text, cancellationToken);
    }

    /// <summary>Stops the loaded conversation and returns to Idle.</summary>
    public void StopSession()
    {
        CancelPendingThinking();
        _tts.StopSpeaking();
        TransitionTo(SpeakingSessionState.Idle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelPendingThinking();
        _tts.StopSpeaking();
        (_tts as IDisposable)?.Dispose();
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private void TransitionTo(SpeakingSessionState next)
    {
        if (_state == next) return;
        _state = next;
        _logger?.LogDebug("Speaking state changed. State={State}", next);
        StateChanged?.Invoke(this, next);
    }

    private void AddMessage(PracticeMessage message)
    {
        _history.Add(message);
        MessageAdded?.Invoke(this, message);
    }

    private void CancelPendingThinking()
    {
        if (_thinkingCts is { } cts)
        {
            cts.Cancel();
            cts.Dispose();
            _thinkingCts = null;
        }
    }
}
