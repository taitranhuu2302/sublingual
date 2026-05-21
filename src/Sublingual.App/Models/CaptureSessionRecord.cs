namespace Sublingual.App.Models;

public sealed class CaptureSessionRecord
{
    public required string SessionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TreePath { get; init; } = string.Empty;
    public required string DirectoryPath { get; init; }
    public required string AudioPath { get; init; }
    public required string TranscriptPath { get; init; }
    public required string MetadataPath { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
