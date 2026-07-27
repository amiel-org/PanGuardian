using PanGuardian.Contracts.Dtos;

namespace PanGuardian.App.ViewModels;

public sealed class MigrationCandidateViewModel
{
    public string AppName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string SuggestedTargetPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string SizeText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool IsProtected { get; init; }
    public bool CanExecuteCopy { get; init; }
    public bool CanSwitch { get; init; }
    public bool CanRollback { get; init; }
    public string OperationHint { get; init; } = string.Empty;

    public MigrationCandidateDto ToDto()
    {
        return new MigrationCandidateDto
        {
            AppName = AppName,
            DisplayName = DisplayName,
            SourcePath = SourcePath,
            SuggestedTargetPath = SuggestedTargetPath,
            SizeBytes = SizeBytes,
            IsProtected = IsProtected,
            Reason = Reason
        };
    }
}
