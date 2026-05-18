using Sublingual.Domain.Audio;

namespace Sublingual.Domain.Sessions;

public sealed record SessionInfo(
    Guid Id,
    AudioSourceType SourceType,
    string SourceLanguage,
    string TargetLanguage,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    SessionState State,
    string? Title
);
