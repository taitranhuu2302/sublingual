using Sublingual.App.Models;

namespace Sublingual.App.ViewModels;

public sealed class SavedTranscriptEntryViewModel
{
    public SavedTranscriptEntryViewModel(SavedTranscriptEntry entry)
    {
        SegmentId = entry.SegmentId;
        OriginalText = entry.OriginalText;
        TranslatedText = entry.TranslatedText;
        IsFinal = entry.IsFinal;
        PartialText = entry.PartialText;
        PartialTranslatedText = entry.PartialTranslatedText;
        FinalText = entry.FinalText;
        FinalTranslatedText = entry.FinalTranslatedText;
        UpdatedAt = entry.UpdatedAt;
    }

    public string SegmentId { get; }
    public string OriginalText { get; }
    public string TranslatedText { get; }
    public bool IsFinal { get; }
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
