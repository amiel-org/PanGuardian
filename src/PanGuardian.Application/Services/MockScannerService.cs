using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MockScannerService : IScannerService
{
    public Task<DashboardSnapshotDto> GetLatestDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new DashboardSnapshotDto
        {
            DriveName = "C:",
            TotalBytes = 512L * 1024 * 1024 * 1024,
            UsedBytes = 441L * 1024 * 1024 * 1024,
            FreeBytes = 71L * 1024 * 1024 * 1024,
            GrowthInLast7DaysBytes = 13L * 1024 * 1024 * 1024,
            TopSources =
            [
                new SourceUsageDto { Name = "微信", SizeBytes = 24L * 1024 * 1024 * 1024, GrowthBytes = 6L * 1024 * 1024 * 1024, Badge = "可迁移" },
                new SourceUsageDto { Name = "企业微信", SizeBytes = 16L * 1024 * 1024 * 1024, GrowthBytes = 4L * 1024 * 1024 * 1024, Badge = "可治理" },
                new SourceUsageDto { Name = "桌面", SizeBytes = 18L * 1024 * 1024 * 1024, GrowthBytes = 2L * 1024 * 1024 * 1024, Badge = "可归档" }
            ],
            Suggestions =
            [
                new ActionSuggestionDto { Title = "先清理安全项", Detail = "临时文件、回收站和缓存预计可释放 6.8 GB。", PrimaryActionLabel = "打开清理中心" },
                new ActionSuggestionDto { Title = "迁移微信媒体到 D 盘", Detail = "不碰聊天记录，优先搬走图片、视频和附件。", PrimaryActionLabel = "打开迁移向导" },
                new ActionSuggestionDto { Title = "归档桌面大文件", Detail = "检测到桌面存在安装包、压缩包和视频类大文件。", PrimaryActionLabel = "查看桌面归档" }
            ]
        };

        return Task.FromResult(snapshot);
    }
}
