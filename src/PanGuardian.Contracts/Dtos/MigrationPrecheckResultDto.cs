namespace PanGuardian.Contracts.Dtos;

public sealed class MigrationPrecheckResultDto
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public bool CanProceed { get; init; }
    public bool HasEnoughSpace { get; init; }
    public bool TargetIsEmptyOrMissing { get; init; }
    public bool IsAppClosed { get; init; }
    public required string Message { get; init; }
    public long RequiredBytes { get; init; }
    public long AvailableBytes { get; init; }
}
