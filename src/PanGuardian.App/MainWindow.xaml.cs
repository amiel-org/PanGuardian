using System.IO;
using System.Windows;
using System.Windows.Input;
using PanGuardian.App.ViewModels;
using PanGuardian.Application.Orchestrators;
using PanGuardian.Application.Services;
using PanGuardian.Contracts.Dtos;

namespace PanGuardian.App;

public partial class MainWindow : Window
{
    private readonly WindowsScannerService _scannerService = new();
    private readonly MigrationPlannerService _migrationPlanner = new();
    private readonly ByteSizeFormatter _formatter = new();
    private readonly CleanupService _cleanupService = new();
    private bool _firstLoadCompleted;

    public MainWindow()
    {
        InitializeComponent();
        ApplyResponsiveWindowSize();
        DataContext = CreateLoadingViewModel();
        Loaded += LoadDashboardAfterWindowShown;
    }

    private void ApplyResponsiveWindowSize()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Min(1460, area.Width - 60);
        Height = Math.Min(920, area.Height - 60);
        MinWidth = Math.Min(1240, area.Width);
        MinHeight = Math.Min(780, area.Height);
    }

    private MainWindowViewModel? CurrentViewModel => DataContext as MainWindowViewModel;

    private MainWindowViewModel CreateLoadingViewModel()
    {
        return new MainWindowViewModel
        {
            Dashboard = DashboardViewModel.CreateLoading(_formatter),
            Migration = MigrationViewModel.CreateLoading(),
            Cleanup = CleanupViewModel.Create()
        };
    }

    private async void LoadDashboardAfterWindowShown(object sender, RoutedEventArgs e)
    {
        if (_firstLoadCompleted)
        {
            return;
        }

        _firstLoadCompleted = true;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var viewModel = CurrentViewModel ?? CreateLoadingViewModel();
        if (DataContext is null)
        {
            DataContext = viewModel;
        }

        viewModel.Dashboard = DashboardViewModel.CreateLoading(_formatter);
        viewModel.Migration = MigrationViewModel.CreateLoading();

        try
        {
            var result = await Task.Run(() =>
            {
                var orchestrator = new DashboardOrchestrator(_scannerService);
                return new
                {
                    Dashboard = DashboardViewModel.Create(orchestrator, _formatter),
                    Migration = MigrationViewModel.Create(_migrationPlanner, _formatter)
                };
            });

            viewModel.Dashboard = result.Dashboard;
            viewModel.Migration = result.Migration;
        }
        catch (Exception ex)
        {
            viewModel.Dashboard = DashboardViewModel.CreateError($"后台扫描失败：{ex.Message}", _formatter);
            viewModel.Migration = MigrationViewModel.CreateError(ex.Message);
        }
    }

    private async void RefreshDashboard(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private async void ScanCleanupCandidates(object sender, RoutedEventArgs e)
    {
        var viewModel = CurrentViewModel;
        if (viewModel is null)
        {
            return;
        }

        MainTabs.SelectedIndex = 3;
        viewModel.Cleanup.SetStatus("正在后台扫描可清理文件，请稍候。");

        try
        {
            var candidates = await Task.Run(_cleanupService.ScanCleanupCandidates);
            viewModel.Cleanup.SetCandidates(candidates, _formatter);
        }
        catch (Exception ex)
        {
            viewModel.Cleanup.SetStatus($"扫描可清理文件失败：{ex.Message}");
            ShowSafeMessage("扫描可清理文件失败", ex.Message, MessageBoxImage.Error);
        }
    }

    private void SelectAllCleanup(object sender, RoutedEventArgs e)
    {
        CurrentViewModel?.Cleanup.SelectAll(_formatter);
    }

    private void UnselectAllCleanup(object sender, RoutedEventArgs e)
    {
        CurrentViewModel?.Cleanup.UnselectAll(_formatter);
    }

    private void GoMigrationCenter(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
    }

    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // 某些触控板/双击场景下 DragMove 可能抛异常，忽略即可，避免影响主流程。
        }
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ExecuteSelectedCleanup(object sender, RoutedEventArgs e)
    {
        var viewModel = CurrentViewModel;
        if (viewModel is null)
        {
            return;
        }

        var selectedItems = viewModel.Cleanup.Candidates
            .Where(item => item.IsSelected)
            .ToList();
        var selected = selectedItems.Select(item => item.FullPath).ToList();

        if (selected.Count == 0)
        {
            ShowSafeMessage("一键清理", "当前没有选中的可清理文件。", MessageBoxImage.Information);
            return;
        }

        var selectedBytes = selectedItems.Sum(item => item.SizeBytes);
        var confirm = MessageBox.Show(
            this,
            $"即将把 {selected.Count} 个文件移入回收站，预计释放 {_formatter.FormatBytes(selectedBytes)}。\n\n不会直接永久删除，但占用中的文件可能处理失败。确定继续吗？",
            "确认清理",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            viewModel.Cleanup.SetStatus("正在清理选中文件，请稍候。");
            var result = await Task.Run(() => _cleanupService.DeleteCandidates(selected));
            ShowSafeMessage("一键清理", result, MessageBoxImage.Information);
            await RefreshCleanupCandidatesAsync();
            await ReloadAsync();
            MainTabs.SelectedIndex = 3;
        }
        catch (Exception ex)
        {
            viewModel.Cleanup.SetStatus($"清理失败：{ex.Message}");
            ShowSafeMessage("清理失败", ex.Message, MessageBoxImage.Error);
        }
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

        var confirm = ConfirmMigrationAction(
            "确认复制迁移",
            "即将把该目录复制到目标路径。复制阶段不会删除原目录，复制校验通过后才允许切换联接。",
            candidateViewModel);
        if (!confirm)
        {
            return;
        }

        var executor = new MigrationExecutorService(
            new MigrationRecordStore(),
            new MigrationRollbackStore(),
            new MigrationPrecheckService());
        var candidate = candidateViewModel.ToDto();

        try
        {
            var result = await Task.Run(() => executor.ExecuteCopy(candidate));
            ShowSafeMessage(result.Success ? "迁移完成" : "迁移结果", BuildMigrationDetail(result), result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            await ReloadAsync();
            MainTabs.SelectedIndex = 2;
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

        var confirm = ConfirmMigrationAction(
            "确认切换联接",
            "即将把原目录重命名为备份目录，并在原路径创建目录联接。请先确认对应软件已退出，且复制结果已核对。",
            candidateViewModel,
            MessageBoxImage.Warning);
        if (!confirm)
        {
            return;
        }

        var switcher = new MigrationSwitchService();
        var candidate = candidateViewModel.ToDto();

        try
        {
            var result = await Task.Run(() => switcher.SwitchToJunction(candidate));
            var detail = $"{result.Message}\n\n原路径：{result.SourcePath}\n目标路径：{result.TargetPath}\n备份路径：{result.BackupPath}";
            ShowSafeMessage(result.Success ? "切换完成" : "切换结果", detail, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            await ReloadAsync();
            MainTabs.SelectedIndex = 2;
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

        var confirm = ConfirmMigrationAction(
            "确认回滚原目录",
            "即将删除原路径处的联接，并把备份目录恢复回原路径。若当前原路径不是联接，程序会拒绝自动回滚。",
            candidateViewModel,
            MessageBoxImage.Warning);
        if (!confirm)
        {
            return;
        }

        var rollback = new MigrationRollbackExecutorService();
        var candidate = candidateViewModel.ToDto();

        try
        {
            var result = await Task.Run(() => rollback.Restore(candidate));
            ShowSafeMessage(result.Success ? "回滚完成" : "回滚结果", result.Message, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            await ReloadAsync();
            MainTabs.SelectedIndex = 2;
        }
        catch (Exception ex)
        {
            ShowSafeMessage("回滚失败", ex.Message, MessageBoxImage.Error);
        }
    }

    private async void RunCleanupAction(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CleanupItemViewModel item })
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            BuildCleanupConfirmText(item.Title),
            item.Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            CurrentViewModel?.Cleanup.SetStatus($"正在执行：{item.Title}，请稍候。");
            var message = await Task.Run(() => item.Title switch
            {
                "临时文件" => _cleanupService.CleanTempFiles(),
                "下载目录旧文件" => _cleanupService.ArchiveOldFiles(
                    "下载目录",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    Path.Combine(@"D:\PanGuardianData\FileArchive", "Downloads")),
                "图片目录旧文件" => _cleanupService.ArchiveOldFiles(
                    "图片目录",
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Path.Combine(@"D:\PanGuardianData\FileArchive", "Pictures")),
                "文档目录旧文件" => _cleanupService.ArchiveOldFiles(
                    "文档目录",
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Path.Combine(@"D:\PanGuardianData\FileArchive", "Documents")),
                "桌面归档" => _cleanupService.ArchiveDesktopLargeFiles(),
                _ => "当前没有可执行动作。"
            });

            ShowSafeMessage(item.Title, message, MessageBoxImage.Information);
            CurrentViewModel?.Cleanup.SetStatus(message);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            CurrentViewModel?.Cleanup.SetStatus($"执行失败：{ex.Message}");
            ShowSafeMessage($"{item.Title}失败", ex.Message, MessageBoxImage.Error);
        }
    }

    private async Task RefreshCleanupCandidatesAsync()
    {
        var refreshed = await Task.Run(_cleanupService.ScanCleanupCandidates);
        CurrentViewModel?.Cleanup.SetCandidates(refreshed, _formatter);
    }

    private bool ConfirmMigrationAction(string title, string description, MigrationCandidateViewModel candidate, MessageBoxImage icon = MessageBoxImage.Question)
    {
        var message = $"{description}\n\n项目：{candidate.DisplayName}\n源路径：{candidate.SourcePath}\n目标路径：{candidate.SuggestedTargetPath}\n大小：{candidate.SizeText}\n\n确定继续吗？";
        return MessageBox.Show(this, message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
    }

    private static string BuildMigrationDetail(MigrationExecutionResultDto result)
    {
        var formatter = new ByteSizeFormatter();
        return $"{result.Message}\n\n源文件数：{result.SourceFileCount}\n目标文件数：{result.TargetFileCount}\n源大小：{formatter.FormatBytes(result.SourceBytes)}\n目标大小：{formatter.FormatBytes(result.TargetBytes)}";
    }

    private static string BuildCleanupConfirmText(string title)
    {
        return title switch
        {
            "临时文件" => "即将扫描系统临时目录，并把能处理的文件移动到回收站。占用中或无权限文件会跳过。确定继续吗？",
            "桌面归档" => "即将把桌面上超过 3 个月未修改且大于 200 MB 的文件移动到 D:\\PanGuardianData\\DesktopArchive。确定继续吗？",
            _ => "即将把符合条件的旧文件移动到 D:\\PanGuardianData\\FileArchive 归档目录。确定继续吗？"
        };
    }

    private void ShowSafeMessage(string title, string message, MessageBoxImage icon)
    {
        MessageBox.Show(this, string.IsNullOrWhiteSpace(message) ? "操作已结束，但没有返回详细信息。" : message, title, MessageBoxButton.OK, icon);
    }
}
