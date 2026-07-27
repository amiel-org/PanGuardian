using System.Windows;
using PanGuardian.App.ViewModels;
using PanGuardian.Application.Services;
using PanGuardian.Contracts.Dtos;

namespace PanGuardian.App.Views;

public partial class MigrationWindow : Window
{
    private readonly MigrationPlannerService _planner = new();
    private readonly ByteSizeFormatter _formatter = new();
    private readonly MigrationExecutorService _executor =
        new(new MigrationRecordStore(), new MigrationRollbackStore(), new MigrationPrecheckService());
    private readonly MigrationSwitchService _switcher = new();
    private readonly MigrationRollbackExecutorService _rollbackExecutor = new();

    public MigrationWindow()
    {
        InitializeComponent();
        Reload();
    }

    private void Reload()
    {
        try
        {
            DataContext = MigrationViewModel.Create(_planner, _formatter);
        }
        catch (Exception ex)
        {
            DataContext = MigrationViewModel.CreateError(ex.Message);
        }
    }

    private MigrationCandidateDto? ResolveCandidate(MigrationCandidateViewModel candidateViewModel)
    {
        var plan = _planner.BuildPlan();
        return plan.Candidates.FirstOrDefault(item =>
            item.SourcePath.Equals(candidateViewModel.SourcePath, StringComparison.OrdinalIgnoreCase));
    }

    private async void ExecuteMigration(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MigrationCandidateViewModel candidateViewModel })
        {
            return;
        }

        if (!candidateViewModel.CanExecuteCopy)
        {
            ShowSafeMessage("迁移不可执行", candidateViewModel.OperationHint, MessageBoxImage.Information);
            return;
        }

        var candidate = ResolveCandidate(candidateViewModel);
        if (candidate is null)
        {
            ShowSafeMessage("迁移向导", "没有找到对应的迁移候选项。", MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmMigrationAction(
                "确认复制迁移",
                "即将把该目录复制到目标路径。复制阶段不会删除原目录，复制校验通过后才允许切换联接。",
                candidateViewModel))
        {
            return;
        }

        try
        {
            var result = await Task.Run(() => _executor.ExecuteCopy(candidate));
            ShowSafeMessage(result.Success ? "迁移完成" : "迁移结果", BuildMigrationDetail(result), result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Reload();
        }
        catch (Exception ex)
        {
            ShowSafeMessage("迁移失败", ex.Message, MessageBoxImage.Error);
        }
    }

    private async void SwitchMigration(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MigrationCandidateViewModel candidateViewModel })
        {
            return;
        }

        if (!candidateViewModel.CanSwitch)
        {
            ShowSafeMessage("切换不可执行", candidateViewModel.OperationHint, MessageBoxImage.Information);
            return;
        }

        var candidate = ResolveCandidate(candidateViewModel);
        if (candidate is null)
        {
            ShowSafeMessage("迁移向导", "没有找到对应的迁移候选项。", MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmMigrationAction(
                "确认切换联接",
                "即将把原目录重命名为备份目录，并在原路径创建目录联接。请先确认对应软件已退出，且复制结果已核对。",
                candidateViewModel,
                MessageBoxImage.Warning))
        {
            return;
        }

        try
        {
            var result = await Task.Run(() => _switcher.SwitchToJunction(candidate));
            var detail = $"{result.Message}\n\n原路径：{result.SourcePath}\n目标路径：{result.TargetPath}\n备份路径：{result.BackupPath}";
            ShowSafeMessage(result.Success ? "切换完成" : "切换结果", detail, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Reload();
        }
        catch (Exception ex)
        {
            ShowSafeMessage("切换失败", ex.Message, MessageBoxImage.Error);
        }
    }

    private async void RollbackMigration(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MigrationCandidateViewModel candidateViewModel })
        {
            return;
        }

        if (!candidateViewModel.CanRollback)
        {
            ShowSafeMessage("回滚不可执行", candidateViewModel.OperationHint, MessageBoxImage.Information);
            return;
        }

        var candidate = ResolveCandidate(candidateViewModel);
        if (candidate is null)
        {
            ShowSafeMessage("迁移向导", "没有找到对应的迁移候选项。", MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmMigrationAction(
                "确认回滚原目录",
                "即将删除原路径处的联接，并把备份目录恢复回原路径。若当前原路径不是联接，程序会拒绝自动回滚。",
                candidateViewModel,
                MessageBoxImage.Warning))
        {
            return;
        }

        try
        {
            var result = await Task.Run(() => _rollbackExecutor.Restore(candidate));
            ShowSafeMessage(result.Success ? "回滚完成" : "回滚结果", result.Message, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            Reload();
        }
        catch (Exception ex)
        {
            ShowSafeMessage("回滚失败", ex.Message, MessageBoxImage.Error);
        }
    }

    private bool ConfirmMigrationAction(string title, string description, MigrationCandidateViewModel candidate, MessageBoxImage icon = MessageBoxImage.Question)
    {
        var message = $"{description}\n\n项目：{candidate.DisplayName}\n源路径：{candidate.SourcePath}\n目标路径：{candidate.SuggestedTargetPath}\n大小：{candidate.SizeText}\n\n确定继续吗？";
        return MessageBox.Show(this, message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
    }

    private string BuildMigrationDetail(MigrationExecutionResultDto result)
    {
        return $"{result.Message}\n\n源文件数：{result.SourceFileCount}\n目标文件数：{result.TargetFileCount}\n源大小：{_formatter.FormatBytes(result.SourceBytes)}\n目标大小：{_formatter.FormatBytes(result.TargetBytes)}";
    }

    private void ShowSafeMessage(string title, string message, MessageBoxImage icon)
    {
        MessageBox.Show(this, string.IsNullOrWhiteSpace(message) ? "操作已结束，但没有返回详细信息。" : message, title, MessageBoxButton.OK, icon);
    }
}
