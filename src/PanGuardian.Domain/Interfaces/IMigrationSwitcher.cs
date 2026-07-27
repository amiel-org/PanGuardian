using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationSwitcher
{
    MigrationSwitchResultDto SwitchToJunction(MigrationCandidateDto candidate);
}
