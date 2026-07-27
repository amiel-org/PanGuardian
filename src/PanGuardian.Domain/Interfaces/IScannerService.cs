using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Domain.Interfaces;

public interface IScannerService
{
    Task<DashboardSnapshotDto> GetLatestDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}
