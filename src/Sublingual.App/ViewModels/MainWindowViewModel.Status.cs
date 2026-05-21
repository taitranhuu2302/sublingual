namespace Sublingual.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void UpdateSpeechToTextStatus()
    {
        if (SelectedSpeechToTextModel is null)
        {
            SpeechToTextStatus = "No local speech model found.";
            return;
        }

        SpeechToTextStatus = BuildSpeechToTextStatus(SelectedSpeechToTextModel.Name);
    }
}
