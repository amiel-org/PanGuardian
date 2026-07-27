namespace PanGuardian.Contracts.Dtos;

public sealed class MigrationSwitchResultDto
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public required string BackupPath { get; init; }
    public bool Success { get; init; }
    public required string Message { get; init; }
}
