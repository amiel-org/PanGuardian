namespace PanGuardian.Contracts.Dtos;

public sealed class MigrationRollbackPointDto
{
    public required string AppName { get; init; }
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public required string BackupHintPath { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
