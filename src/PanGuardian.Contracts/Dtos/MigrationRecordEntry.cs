namespace PanGuardian.Contracts.Dtos;

public sealed class MigrationRecordEntry
{
    public required string AppName { get; init; }
    public required string DisplayName { get; init; }
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public required string Message { get; init; }
    public bool Success { get; init; }
    public long SourceFileCount { get; init; }
    public long TargetFileCount { get; init; }
    public long SourceBytes { get; init; }
    public long TargetBytes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
