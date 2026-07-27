using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationRollbackExecutor
{
    MigrationRollbackResultDto Restore(MigrationCandidateDto candidate);
}
