using System.Collections.ObjectModel;
using System.IO;
using PanGuardian.Application.Orchestrators;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.App.ViewModels;

public sealed class DashboardViewModel
{
    public string DriveName { get; private init; } = "C:";
    public string FreeSpaceText { get; private init; } = "0 GB";
    public string UsedSpaceSummary { get; private init; } = string.Empty;
    public string GrowthText { get; private init; } = "0 GB";
    public double UsedPercent { get; private init; }
    public string UsedPercentText => $"{UsedPercent:0}%";
    public ObservableCollection<SourceUsageCardViewModel> TopSources { get; } = [];
    public ObservableCollection<SourceDetailCardViewModel> SourceDetails { get; } = [];
    public ObservableCollection<LargeFileCardViewModel> LargeFiles { get; } = [];
    public ObservableCollection<ActionSuggestionViewModel> Suggestions { get; } = [];

    public static DashboardViewModel CreateLoading(IStorageFormatter formatter)
    {
        var systemDriveRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var drive = new DriveInfo(systemDriveRoot);

        var viewModel = new DashboardViewModel
        {
            DriveName = $"{drive.Name.TrimEnd('\\')} 系统盘",
            FreeSpaceText = formatter.FormatBytes(drive.AvailableFreeSpace),
            UsedSpaceSummary = $"已用 {formatter.FormatBytes(drive.TotalSize - drive.AvailableFreeSpace)} / 总计 {formatter.FormatBytes(drive.TotalSize)}",
            UsedPercent = drive.TotalSize <= 0 ? 0 : Math.Clamp(((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100, 0, 100),
            GrowthText = "扫描中"
        };

        viewModel.Suggestions.Add(new ActionSuggestionViewModel
        {
            Title = "正在扫描本机重点目录",
            Detail = "窗口已先打开，微信、企业微信、桌面、下载和临时目录会在后台继续统计，避免启动时像卡死一样没有响应。",
            PrimaryActionLabel = "请稍候"
        });

        return viewModel;
    }

    public static DashboardViewModel CreateError(string message, IStorageFormatter formatter)
    {
        var viewModel = CreateLoading(formatter);
        viewModel.Suggestions.Clear();
        viewModel.Suggestions.Add(new ActionSuggestionViewModel
        {
            Title = "扫描未完成",
            Detail = message,
            PrimaryActionLabel = "重新扫描"
        });
        return viewModel;
    }

    public static DashboardViewModel Create(
        DashboardOrchestrator orchestrator,
        IStorageFormatter formatter)
    {
        var snapshot = orchestrator.LoadAsync().GetAwaiter().GetResult();
        return FromSnapshot(snapshot, formatter);
    }

    private static DashboardViewModel FromSnapshot(
        DashboardSnapshotDto snapshot,
        IStorageFormatter formatter)
    {
        var viewModel = new DashboardViewModel
        {
            DriveName = $"{snapshot.DriveName} 系统盘",
            FreeSpaceText = formatter.FormatBytes(snapshot.FreeBytes),
            UsedSpaceSummary = $"已用 {formatter.FormatBytes(snapshot.UsedBytes)} / 总计 {formatter.FormatBytes(snapshot.TotalBytes)}",
            UsedPercent = snapshot.TotalBytes <= 0 ? 0 : Math.Clamp(((double)snapshot.UsedBytes / snapshot.TotalBytes) * 100, 0, 100),
            GrowthText = $"+{formatter.FormatBytes(snapshot.GrowthInLast7DaysBytes)}"
        };

        var maxSourceBytes = snapshot.TopSources.Count == 0 ? 0 : snapshot.TopSources.Max(item => item.SizeBytes);

        foreach (var item in snapshot.TopSources)
        {
            var percent = maxSourceBytes <= 0
                ? 0
                : Math.Clamp((double)item.SizeBytes / maxSourceBytes * 100, 0, 100);

            viewModel.TopSources.Add(new SourceUsageCardViewModel
            {
                Name = item.Name,
                Badge = item.Badge,
                SizeText = $"当前占用 {formatter.FormatBytes(item.SizeBytes)}",
                SizePercent = percent,
                SizePercentText = $"{percent:0}%",
                GrowthText = $"最近 7 天变化 {formatter.FormatBytes(item.GrowthBytes)}",
                PathHint = string.IsNullOrWhiteSpace(item.PrimaryPath) ? string.Empty : item.PrimaryPath,
                ActionLabel = item.Badge switch
                {
                    "可迁移" => "去迁移",
                    "可归档" => "去归档",
                    "可清理" => "去清理",
                    _ => "去查看"
                }
            });
        }

        foreach (var item in snapshot.SourceDetails.Take(6))
        {
            viewModel.SourceDetails.Add(new SourceDetailCardViewModel
            {
                SourceName = item.SourceName,
                PathText = item.Path,
                SizeText = formatter.FormatBytes(item.SizeBytes),
                GrowthText = $"+{formatter.FormatBytes(item.GrowthBytes)}"
            });
        }

        foreach (var file in snapshot.LargeFiles.Take(6))
        {
            viewModel.LargeFiles.Add(new LargeFileCardViewModel
            {
                FileName = file.FileName,
                Category = file.Category,
                PathText = file.FullPath,
                SizeText = formatter.FormatBytes(file.SizeBytes),
                ModifiedText = file.LastModifiedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            });
        }

        foreach (var suggestion in snapshot.Suggestions)
        {
            viewModel.Suggestions.Add(new ActionSuggestionViewModel
            {
                Title = suggestion.Title,
                Detail = suggestion.Detail,
                PrimaryActionLabel = suggestion.PrimaryActionLabel
            });
        }

        return viewModel;
    }
}
