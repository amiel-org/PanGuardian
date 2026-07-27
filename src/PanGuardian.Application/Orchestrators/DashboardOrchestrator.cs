using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Orchestrators;

public sealed class DashboardOrchestrator
{
    private readonly IScannerService _scannerService;

    public DashboardOrchestrator(IScannerService scannerService)
    {
        _scannerService = scannerService;
    }

    public Task<DashboardSnapshotDto> LoadAsync(CancellationToken cancellationToken = default)
    {
        return _scannerService.GetLatestDashboardSnapshotAsync(cancellationToken);
    }
}
