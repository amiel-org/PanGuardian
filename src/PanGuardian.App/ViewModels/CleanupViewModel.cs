using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.App.ViewModels;

public sealed class CleanupViewModel : INotifyPropertyChanged
{
    private string _summaryText = "点击“一键清理”或“扫描可清理文件”后，将列出可删除文件。";
    private IStorageFormatter? _formatter;

    public ObservableCollection<CleanupItemViewModel> Items { get; } = [];
    public ObservableCollection<CleanupCandidateFileViewModel> Candidates { get; } = [];

    public string SummaryText
    {
        get => _summaryText;
        private set
        {
            if (_summaryText == value)
            {
                return;
            }

            _summaryText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static CleanupViewModel Create()
    {
        var viewModel = new CleanupViewModel();
        viewModel.Items.Add(new CleanupItemViewModel
        {
            Title = "临时文件",
            Description = "优先处理 `%TEMP%`、本地 Temp 和 Windows Temp。",
            SizeText = "可直接执行",
            ActionLabel = "立即清理"
        });
        viewModel.Items.Add(new CleanupItemViewModel
        {
            Title = "下载目录旧文件",
            Description = "归档下载目录中超过 3 个月未修改的旧文件。",
            SizeText = "可执行归档",
            ActionLabel = "立即归档"
        });
        viewModel.Items.Add(new CleanupItemViewModel
        {
            Title = "图片目录旧文件",
            Description = "归档图片目录中超过 3 个月未修改的旧文件。",
            SizeText = "可执行归档",
            ActionLabel = "立即归档"
        });
        viewModel.Items.Add(new CleanupItemViewModel
        {
            Title = "文档目录旧文件",
            Description = "归档文档目录中超过 3 个月未修改的旧文件。",
            SizeText = "可执行归档",
            ActionLabel = "立即归档"
        });
        viewModel.Items.Add(new CleanupItemViewModel
        {
            Title = "桌面归档",
            Description = "归档桌面上超过 3 个月未修改且大于 200 MB 的文件到 D 盘。",
            SizeText = "可执行归档",
            ActionLabel = "立即归档"
        });
        return viewModel;
    }

    public void SetStatus(string message)
    {
        SummaryText = message;
    }

    public void SetCandidates(IEnumerable<CleanupCandidateDto> candidates, IStorageFormatter formatter)
    {
        DetachCandidateHandlers();
        _formatter = formatter;
        Candidates.Clear();
        foreach (var item in candidates)
        {
            var ageDays = Math.Max(0, (DateTime.UtcNow - item.LastModifiedAtUtc).Days);
            var candidate = new CleanupCandidateFileViewModel
            {
                IsSelected = true,
                Category = item.Category,
                FileName = item.FileName,
                FullPath = item.FullPath,
                SizeBytes = item.SizeBytes,
                SizeText = formatter.FormatBytes(item.SizeBytes),
                AgeText = $"{ageDays} 天未修改"
            };

            candidate.PropertyChanged += CandidateOnPropertyChanged;
            Candidates.Add(candidate);
        }

        UpdateSummary(formatter);
    }

    public void SelectAll(IStorageFormatter formatter)
    {
        foreach (var item in Candidates)
        {
            item.IsSelected = true;
        }

        UpdateSummary(formatter);
    }

    public void UnselectAll(IStorageFormatter formatter)
    {
        foreach (var item in Candidates)
        {
            item.IsSelected = false;
        }

        UpdateSummary(formatter);
    }

    public void UpdateSummary(IStorageFormatter formatter)
    {
        if (Candidates.Count == 0)
        {
            SummaryText = "没有扫描到可清理文件。";
            return;
        }

        var selectedCount = Candidates.Count(item => item.IsSelected);
        var selectedBytes = Candidates.Where(item => item.IsSelected).Sum(item => item.SizeBytes);
        SummaryText = $"已扫描到 {Candidates.Count} 个可清理文件，当前选中 {selectedCount} 个，预计可释放 {formatter.FormatBytes(selectedBytes)}。";
    }

    private void CandidateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CleanupCandidateFileViewModel.IsSelected) || _formatter is null)
        {
            return;
        }

        UpdateSummary(_formatter);
    }

    private void DetachCandidateHandlers()
    {
        foreach (var item in Candidates)
        {
            item.PropertyChanged -= CandidateOnPropertyChanged;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
