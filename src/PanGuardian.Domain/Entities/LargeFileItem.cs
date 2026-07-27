namespace PanGuardian.Domain.Entities;

public sealed class LargeFileItem
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public required string Category { get; init; }
    public DateTime LastModifiedAt { get; init; }
}
