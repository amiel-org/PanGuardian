namespace PanGuardian.Contracts.Dtos;

public sealed class MigrationPlanDto
{
    public IReadOnlyList<MigrationCandidateDto> Candidates { get; init; } = [];
}
