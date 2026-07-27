using System.IO;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class WindowsScannerService : IScannerService
{
    private const int MaxLargeFiles = 12;
    private const int MaxFilesPerSourcePath = 20000;
    private const int MaxDirectoriesPerSourcePath = 5000;

    public Task<DashboardSnapshotDto> GetLatestDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => BuildSnapshot(cancellationToken), cancellationToken);
    }

    private static DashboardSnapshotDto BuildSnapshot(CancellationToken cancellationToken)
    {
        var systemDriveRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var drive = new DriveInfo(systemDriveRoot);
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var downloads = Path.Combine(userProfile, "Downloads");

        var largeFiles = new List<LargeFileDto>();
        var sourceResults = new List<SourceScanResult>
        {
            ScanSource("微信", "可迁移", cutoff, largeFiles, cancellationToken,
                Path.Combine(documents, "WeChat Files"),
                Path.Combine(userProfile, "Documents", "WeChat Files"),
                Path.Combine(appDataRoaming, "Tencent", "WeChat"),
                Path.Combine(appDataLocal, "Tencent", "WeChat"),
                Path.Combine(appDataRoaming, "Tencent", "Weixin")),
            ScanSource("企业微信", "可迁移", cutoff, largeFiles, cancellationToken,
                Path.Combine(appDataRoaming, "Tencent", "WXWork"),
                Path.Combine(appDataLocal, "Tencent", "WXWork"),
                Path.Combine(documents, "WXWork"),
                Path.Combine(userProfile, "Documents", "WXWork")),
            ScanSource("桌面", "可归档", cutoff, largeFiles, cancellationToken, desktop),
            ScanSource("下载", "可整理", cutoff, largeFiles, cancellationToken, downloads),
            ScanSource("临时文件", "可清理", cutoff, largeFiles, cancellationToken,
                Path.GetTempPath(),
                Path.Combine(appDataLocal, "Temp"),
                Path.Combine(systemDriveRoot, "Windows", "Temp"))
        };

        return new DashboardSnapshotDto
        {
            DriveName = drive.Name.TrimEnd('\\'),
            TotalBytes = drive.TotalSize,
            UsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
            FreeBytes = drive.AvailableFreeSpace,
            GrowthInLast7DaysBytes = sourceResults.Sum(item => item.GrowthBytes),
            TopSources = sourceResults.OrderByDescending(item => item.SizeBytes).Take(4).Select(item => new SourceUsageDto
            {
                Name = item.Name,
                SizeBytes = item.SizeBytes,
                GrowthBytes = item.GrowthBytes,
                Badge = item.Badge,
                PrimaryPath = item.PrimaryPath
            }).ToList(),
            SourceDetails = sourceResults.SelectMany(item => item.Details).OrderByDescending(item => item.SizeBytes).ToList(),
            LargeFiles = largeFiles.OrderByDescending(item => item.SizeBytes).Take(6).ToList(),
            Suggestions = BuildSuggestions(sourceResults)
        };
    }

    private static IReadOnlyList<ActionSuggestionDto> BuildSuggestions(IEnumerable<SourceScanResult> sources)
    {
        var sourceList = sources.ToList();
        var suggestions = new List<ActionSuggestionDto>();

        var temp = sourceList.FirstOrDefault(item => item.Name == "临时文件");
        if (temp is not null && temp.SizeBytes > 512L * 1024 * 1024)
        {
            suggestions.Add(new ActionSuggestionDto
            {
                Title = "先清理临时文件",
                Detail = $"当前临时文件大约占用 {FormatForSuggestion(temp.SizeBytes)}，这是最安全的第一步。",
                PrimaryActionLabel = "打开清理中心"
            });
        }

        var wechat = sourceList.FirstOrDefault(item => item.Name == "微信");
        if (wechat is not null && wechat.SizeBytes > 1024L * 1024 * 1024)
        {
            suggestions.Add(new ActionSuggestionDto
            {
                Title = "迁移微信附件与媒体",
                Detail = $"微信相关目录当前约 {FormatForSuggestion(wechat.SizeBytes)}，优先搬到 D 盘更稳妥。",
                PrimaryActionLabel = "打开迁移向导"
            });
        }

        var desktop = sourceList.FirstOrDefault(item => item.Name == "桌面");
        if (desktop is not null && desktop.SizeBytes > 1024L * 1024 * 1024)
        {
            suggestions.Add(new ActionSuggestionDto
            {
                Title = "归档桌面大文件",
                Detail = $"桌面当前约 {FormatForSuggestion(desktop.SizeBytes)}，适合按月份或类型归档到 D 盘。",
                PrimaryActionLabel = "查看桌面归档"
            });
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new ActionSuggestionDto
            {
                Title = "开始首次深度扫描",
                Detail = "当前关键目录没有明显超大占用，下一步适合继续补全全盘扫描能力。",
                PrimaryActionLabel = "立即扫描"
            });
        }

        return suggestions.Take(3).ToList();
    }

    private static SourceScanResult ScanSource(
        string name,
        string badge,
        DateTime cutoff,
        List<LargeFileDto> largeFiles,
        CancellationToken cancellationToken,
        params string[] candidatePaths)
    {
        long totalBytes = 0;
        long recentBytes = 0;
        var details = new List<SourceDetailDto>();
        string? primaryPath = null;

        foreach (var path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                continue;
            }

            primaryPath ??= path;
            var (sizeBytes, growthBytes, discoveredFiles) = MeasureDirectory(path, cutoff, cancellationToken);
            totalBytes += sizeBytes;
            recentBytes += growthBytes;
            details.Add(new SourceDetailDto
            {
                SourceName = name,
                Path = path,
                SizeBytes = sizeBytes,
                GrowthBytes = growthBytes
            });

            largeFiles.AddRange(discoveredFiles);
            if (largeFiles.Count > MaxLargeFiles * 3)
            {
                largeFiles.Sort(static (left, right) => right.SizeBytes.CompareTo(left.SizeBytes));
                largeFiles.RemoveRange(MaxLargeFiles * 2, largeFiles.Count - MaxLargeFiles * 2);
            }
        }

        return new SourceScanResult(name, badge, primaryPath, totalBytes, recentBytes, details);
    }

    private static (long SizeBytes, long GrowthBytes, List<LargeFileDto> LargeFiles) MeasureDirectory(
        string rootPath,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        long sizeBytes = 0;
        long growthBytes = 0;
        var scannedFiles = 0;
        var scannedDirectories = 0;
        var largeFiles = new List<LargeFileDto>();
        var directories = new Stack<string>();
        directories.Push(rootPath);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scannedFiles >= MaxFilesPerSourcePath || scannedDirectories >= MaxDirectoriesPerSourcePath)
            {
                break;
            }

            var current = directories.Pop();

            if (IsReparsePoint(current))
            {
                continue;
            }

            try
            {
                var files = Directory.EnumerateFiles(current).ToArray();
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (scannedFiles >= MaxFilesPerSourcePath)
                    {
                        break;
                    }

                    try
                    {
                        if (IsReparsePoint(file))
                        {
                            continue;
                        }

                        var info = new FileInfo(file);
                        scannedFiles++;
                        sizeBytes += info.Length;
                        if (info.LastWriteTimeUtc >= cutoff)
                        {
                            growthBytes += info.Length;
                        }

                        if (info.Length >= 200L * 1024 * 1024)
                        {
                            largeFiles.Add(new LargeFileDto
                            {
                                FileName = info.Name,
                                FullPath = info.FullName,
                                Category = CategorizeFile(info),
                                SizeBytes = info.Length,
                                LastModifiedAtUtc = info.LastWriteTimeUtc
                            });
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                var childDirectories = Directory.EnumerateDirectories(current).ToArray();
                foreach (var directory in childDirectories)
                {
                    if (scannedDirectories >= MaxDirectoriesPerSourcePath)
                    {
                        break;
                    }

                    if (IsReparsePoint(directory))
                    {
                        continue;
                    }

                    scannedDirectories++;
                    directories.Push(directory);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return (sizeBytes, growthBytes, largeFiles);
    }


    private static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    private static string CategorizeFile(FileInfo file)
    {
        var extension = file.Extension.ToLowerInvariant();
        return extension switch
        {
            ".zip" or ".rar" or ".7z" => "压缩包",
            ".mp4" or ".mkv" or ".mov" or ".avi" => "视频",
            ".jpg" or ".jpeg" or ".png" or ".bmp" => "图片",
            ".exe" or ".msi" => "安装包",
            ".pdf" or ".pptx" or ".docx" or ".xlsx" => "文档",
            _ => "文件"
        };
    }

    private static string FormatForSuggestion(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private sealed record SourceScanResult(
        string Name,
        string Badge,
        string? PrimaryPath,
        long SizeBytes,
        long GrowthBytes,
        IReadOnlyList<SourceDetailDto> Details);
}
