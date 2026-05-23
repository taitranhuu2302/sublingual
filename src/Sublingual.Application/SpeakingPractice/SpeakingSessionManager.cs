using Sublingual.Domain.SpeakingPractice;

namespace Sublingual.Application.SpeakingPractice;

/// <summary>
/// Orchestrates the full speaking practice loop:
/// Mic capture → Vosk STT → AI LLM → TTS playback.
/// Owns the session state machine and publishes events for the ViewModel to observe.
/// </summary>
public sealed class SpeakingSessionManager : IDisposable
{
    private readonly IAiTutorService _aiTutor;
    private readonly ITtsService _tts;
    private readonly IMicrophoneTranscriptionService _micTranscription;

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

    public string Topic { get; private set; } = string.Empty;
    public string LanguageLevel { get; private set; } = "Intermediate";
    public SpeakingSessionState State => _state;
    public IReadOnlyList<PracticeMessage> History => _history;

    public SpeakingSessionManager(
        IAiTutorService aiTutor,
        ITtsService tts,
        IMicrophoneTranscriptionService micTranscription)
    {
        _aiTutor = aiTutor;
        _tts = tts;
        _micTranscription = micTranscription;
        _micTranscription.FinalTranscriptReady += OnFinalTranscriptReady;
    }

    /// <summary>Starts a new session, clears history, and transitions to Listening.</summary>
    public void StartSession(string topic, string languageLevel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _history.Clear();
        Topic = topic;
        LanguageLevel = languageLevel;
        TransitionTo(SpeakingSessionState.Listening);
        _ = _micTranscription.StartAsync();
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
                Topic,
                LanguageLevel,
                _history,
                transcript,
                linkedToken
            );
        }
        catch (OperationCanceledException)
        {
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }
        catch
        {
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        if (tutorResponse is null)
        {
            TransitionTo(SpeakingSessionState.Listening);
            return;
        }

        // Patch user message with enhancement advice (immutable record — replace in history).
        if (!string.IsNullOrWhiteSpace(tutorResponse.EnglishEnhancement))
        {
            var enhanced = userMessage with { EnhancementAdvice = tutorResponse.EnglishEnhancement };
            var idx = _history.IndexOf(userMessage);
            if (idx >= 0)
            {
                _history[idx] = enhanced;
                MessageAdded?.Invoke(this, enhanced);
            }
        }

        // Publish AI reply message.
        var aiMessage = new PracticeMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: MessageSender.Ai,
            Text: tutorResponse.TutorReply,
            EnhancementAdvice: null,
            Timestamp: DateTimeOffset.Now
        );
        AddMessage(aiMessage);

        // Publish suggestions.
        SuggestionsUpdated?.Invoke(this, tutorResponse.Suggestions);

        // Speak the AI reply — mute mic to avoid echo.
        _micTranscription.SetMuted(true);
        TransitionTo(SpeakingSessionState.AiSpeaking);
        try
        {
            await _tts.SpeakAsync(tutorResponse.TutorReply, linkedToken);
        }
        catch (OperationCanceledException)
        {
            // User skipped or started speaking.
        }
        finally
        {
            _micTranscription.SetMuted(false);
        }

        if (!linkedToken.IsCancellationRequested)
        {
            TransitionTo(SpeakingSessionState.Listening);
        }
    }

    /// <summary>Stops the session and returns to Idle.</summary>
    public void StopSession()
    {
        CancelPendingThinking();
        _tts.StopSpeaking();
        _ = _micTranscription.StopAsync();
        TransitionTo(SpeakingSessionState.Idle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelPendingThinking();
        _tts.StopSpeaking();
        _micTranscription.FinalTranscriptReady -= OnFinalTranscriptReady;
        (_micTranscription as IDisposable)?.Dispose();
        (_tts as IDisposable)?.Dispose();
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private void TransitionTo(SpeakingSessionState next)
    {
        if (_state == next) return;
        _state = next;
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

    private void OnFinalTranscriptReady(object? sender, string transcript)
    {
        // Fire-and-forget: don't block the capture callback thread.
        _ = HandleUserTranscriptAsync(transcript);
    }
}
