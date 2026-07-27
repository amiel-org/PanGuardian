using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.App.ViewModels;

public sealed class MigrationViewModel
{
    public ObservableCollection<MigrationCandidateViewModel> Candidates { get; } = [];

    public string SummaryText { get; private set; } = "未找到可迁移目录，或 D 盘不可用。";

    public static MigrationViewModel CreateLoading()
    {
        return new MigrationViewModel
        {
            SummaryText = "正在后台识别微信 / 企业微信可迁移目录，并检查 D 盘是否可用，请稍候。"
        };
    }

    public static MigrationViewModel CreateError(string message)
    {
        return new MigrationViewModel
        {
            SummaryText = $"迁移目录识别未完成：{message}"
        };
    }

    public static MigrationViewModel Create(IMigrationPlanner planner, IStorageFormatter formatter)
    {
        var plan = planner.BuildPlan();
        var viewModel = new MigrationViewModel();
        var statuses = ReadStatuses();

        foreach (var item in plan.Candidates)
        {
            var backupPath = item.SourcePath + ".pangd.backup";
            var hasBackup = Directory.Exists(backupPath);
            var isSwitched = IsJunction(item.SourcePath) && hasBackup;
            var hasTargetData = SafeHasEntries(item.SuggestedTargetPath);
            var statusText = GetStatusText(item, statuses, isSwitched, hasBackup);
            var canExecuteCopy = !item.IsProtected && !isSwitched && !hasTargetData;
            var canSwitch = !item.IsProtected
                && !isSwitched
                && hasTargetData
                && (statusText is "已复制" or "可切换 / 可回滚");
            var canRollback = hasBackup;

            viewModel.Candidates.Add(new MigrationCandidateViewModel
            {
                AppName = item.AppName,
                DisplayName = item.DisplayName,
                SourcePath = item.SourcePath,
                SuggestedTargetPath = item.SuggestedTargetPath,
                SizeBytes = item.SizeBytes,
                SizeText = formatter.FormatBytes(item.SizeBytes),
                StatusText = statusText,
                Reason = item.Reason,
                IsProtected = item.IsProtected,
                CanExecuteCopy = canExecuteCopy,
                CanSwitch = canSwitch,
                CanRollback = canRollback,
                OperationHint = BuildOperationHint(item, statusText, canExecuteCopy, canSwitch, canRollback, isSwitched)
            });
        }

        if (viewModel.Candidates.Count > 0)
        {
            var migratable = plan.Candidates.Where(item => !item.IsProtected).Sum(item => item.SizeBytes);
            var switchableCount = viewModel.Candidates.Count(item => item.CanSwitch);
            var rollbackableCount = viewModel.Candidates.Count(item => item.CanRollback);
            var protectedCount = viewModel.Candidates.Count(item => item.IsProtected);
            viewModel.SummaryText = $"已识别 {plan.Candidates.Count} 个候选目录，建议优先迁移 {formatter.FormatBytes(migratable)}；当前可切换 {switchableCount} 项，可回滚 {rollbackableCount} 项。";
            if (protectedCount > 0)
            {
                viewModel.SummaryText += $" 其中 {protectedCount} 项受保护，只展示不自动处理。";
            }
        }

        return viewModel;
    }

    private static string GetStatusText(
        MigrationCandidateDto candidate,
        IReadOnlyDictionary<string, string> statuses,
        bool isSwitched,
        bool hasBackup)
    {
        if (candidate.IsProtected)
        {
            return "受保护";
        }

        if (isSwitched)
        {
            return "已切换 / 可回滚";
        }

        if (statuses.TryGetValue(candidate.SourcePath, out var storedStatus))
        {
            return storedStatus;
        }

        if (hasBackup)
        {
            return "可回滚";
        }

        return "待复制";
    }

    private static string BuildOperationHint(
        MigrationCandidateDto candidate,
        string statusText,
        bool canExecuteCopy,
        bool canSwitch,
        bool canRollback,
        bool isSwitched)
    {
        if (candidate.IsProtected)
        {
            return "该目录包含配置或业务数据，当前版本默认只展示，不自动迁移。";
        }

        if (isSwitched)
        {
            return "已完成联接切换；若后续应用异常，可执行回滚恢复原目录。";
        }

        if (canRollback)
        {
            return "已检测到备份目录，必要时可以回滚恢复。";
        }

        if (canSwitch)
        {
            return "已完成复制且目标目录存在内容，可以继续切换联接。";
        }

        if (statusText == "待复制" && !canExecuteCopy)
        {
            return "目标路径已存在内容，当前不会覆盖复制；请先人工确认目标目录。";
        }

        if (statusText == "复制失败")
        {
            return "上次复制未完成，建议先检查空间、占用状态和目标路径。";
        }

        if (canExecuteCopy)
        {
            return "建议先执行复制；复制校验通过后，再继续切换联接。";
        }

        return "当前条件未满足，请先完成前置检查。";
    }

    private static bool SafeHasEntries(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsJunction(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> ReadStatuses()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var recordFile = Path.Combine(baseDir, "migration-records.jsonl");
        if (File.Exists(recordFile))
        {
            foreach (var line in File.ReadLines(recordFile))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<MigrationRecordEntry>(line);
                    if (item is not null)
                    {
                        result[item.SourcePath] = item.Success ? "已复制" : "复制失败";
                    }
                }
                catch
                {
                }
            }
        }

        var rollbackFile = Path.Combine(baseDir, "migration-rollback-points.jsonl");
        if (File.Exists(rollbackFile))
        {
            foreach (var line in File.ReadLines(rollbackFile))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<MigrationRollbackPointDto>(line);
                    if (item is not null)
                    {
                        result[item.SourcePath] = "可切换 / 可回滚";
                    }
                }
                catch
                {
                }
            }
        }

        return result;
    }
}
