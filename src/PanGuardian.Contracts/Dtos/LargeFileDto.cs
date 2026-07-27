namespace PanGuardian.Contracts.Dtos;

public sealed class LargeFileDto
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public required string Category { get; init; }
    public long SizeBytes { get; init; }
    public DateTime LastModifiedAtUtc { get; init; }
}
