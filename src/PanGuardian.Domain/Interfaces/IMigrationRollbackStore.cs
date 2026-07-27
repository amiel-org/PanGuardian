using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationRollbackStore
{
    void Append(MigrationRollbackPointDto point);
}
