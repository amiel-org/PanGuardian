namespace PanGuardian.Contracts.Dtos;

public sealed class MigrationCandidateDto
{
    public required string AppName { get; init; }
    public required string DisplayName { get; init; }
    public required string SourcePath { get; init; }
    public required string SuggestedTargetPath { get; init; }
    public long SizeBytes { get; init; }
    public bool IsProtected { get; init; }
    public required string Reason { get; init; }
}
