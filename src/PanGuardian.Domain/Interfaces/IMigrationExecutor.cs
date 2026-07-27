using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationExecutor
{
    MigrationExecutionResultDto ExecuteCopy(MigrationCandidateDto candidate);
}
