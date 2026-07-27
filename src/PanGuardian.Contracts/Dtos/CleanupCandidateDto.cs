namespace PanGuardian.Contracts.Dtos;

public sealed class CleanupCandidateDto
{
    public required string Category { get; init; }
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
    public DateTime LastModifiedAtUtc { get; init; }
}
