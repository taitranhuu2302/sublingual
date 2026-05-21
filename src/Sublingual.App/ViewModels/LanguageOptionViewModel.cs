namespace Sublingual.App.ViewModels;

public sealed class LanguageOptionViewModel(string name, string code)
{
    public string Name { get; } = name;
    public string Code { get; } = code;
}
