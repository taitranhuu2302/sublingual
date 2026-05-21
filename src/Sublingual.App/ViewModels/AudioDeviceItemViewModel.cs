using Sublingual.Domain.Audio;

namespace Sublingual.App.ViewModels;

public sealed class AudioDeviceItemViewModel(AudioDevice device)
{
    public string Id { get; } = device.Id;
    public string Name { get; } = device.Name;
    public bool IsDefault { get; } = device.IsDefault;
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;
}
