using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IMigrationPlanner
{
    MigrationPlanDto BuildPlan();
}
