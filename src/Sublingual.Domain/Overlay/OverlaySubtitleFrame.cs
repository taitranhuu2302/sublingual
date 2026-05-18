namespace Sublingual.Domain.Overlay;

public sealed record OverlaySubtitleFrame(
    string? PartialText,
    string? OriginalText,
    string? TranslatedText,
    DateTimeOffset Timestamp
);
