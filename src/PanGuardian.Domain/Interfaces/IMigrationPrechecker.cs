using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationPrechecker
{
    MigrationPrecheckResultDto Check(MigrationCandidateDto candidate);
}
