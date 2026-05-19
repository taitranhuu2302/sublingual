namespace Sublingual.App.Models;

public sealed class CaptureSessionMetadata
{
    public string ModelName { get; set; } = "Unknown";
    public string DeviceName { get; set; } = "Unknown";
    public string Language { get; set; } = "en";
    public double DurationSeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
