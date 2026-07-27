using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationRecordStore
{
    void Append(MigrationRecordEntry entry);
}
