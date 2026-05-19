namespace Sublingual.App.Models;

public sealed class SpeechToTextModelOption
{
    public required string Name { get; init; }
    public required string Path { get; init; }

    public override string ToString() => Name;
}
