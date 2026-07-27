using System.IO;
using System.Runtime.InteropServices;
using PanGuardian.Contracts.Dtos;

namespace PanGuardian.Application.Services;

public sealed class CleanupService
{
    private static readonly TimeSpan OldFileThreshold = TimeSpan.FromDays(90);
    private const int MaxScanFilesPerRoot = 20_000;
    private const int MaxQuickCleanFilesPerRoot = 5_000;
    private const int MaxArchiveCandidates = 50;
    private const int MaxDirectoriesPerRoot = 5_000;
    private const long DesktopLargeFileThresholdBytes = 200L * 1024 * 1024;

    public IReadOnlyList<CleanupCandidateDto> ScanCleanupCandidates()
    {
        var results = new List<CleanupCandidateDto>();
        var cutoff = DateTime.UtcNow.Subtract(OldFileThreshold);

        AddCandidates(results, "临时文件", Path.GetTempPath(), cutoff);
        AddCandidates(results, "下载目录", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), cutoff);
        AddCandidates(results, "图片目录", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), cutoff);
        AddCandidates(results, "文档目录", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), cutoff);

        return results
            .OrderByDescending(item => item.SizeBytes)
            .Take(500)
            .ToList();
    }

    public string DeleteCandidates(IEnumerable<string> filePaths)
    {
        long deletedBytes = 0;
        var deletedFiles = 0;
        var skippedFiles = 0;
        var failedFiles = 0;

        foreach (var path in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(path))
                {
                    skippedFiles++;
                    continue;
                }

                var info = new FileInfo(path);
                var size = info.Length;
                SendFileToRecycleBin(path);
                deletedBytes += size;
                deletedFiles++;
            }
            catch (OperationCanceledException)
            {
                failedFiles++;
            }
            catch
            {
                failedFiles++;
            }
        }

        return BuildDeleteSummary("清理", deletedFiles, deletedBytes, skippedFiles, failedFiles);
    }

    public string CleanTempFiles()
    {
        var paths = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Windows", "Temp")
        };

        long deletedBytes = 0;
        var deletedFiles = 0;
        var failedFiles = 0;
        var scannedFiles = 0;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in SafeEnumerateFiles(path, MaxQuickCleanFilesPerRoot))
            {
                scannedFiles++;
                try
                {
                    if (!File.Exists(file))
                    {
                        continue;
                    }

                    var info = new FileInfo(file);
                    var size = info.Length;
                    SendFileToRecycleBin(file);
                    deletedBytes += size;
                    deletedFiles++;
                }
                catch (OperationCanceledException)
                {
                    failedFiles++;
                }
                catch
                {
                    failedFiles++;
                }
            }
        }

        return BuildDeleteSummary("临时文件清理", deletedFiles, deletedBytes, skippedFiles: 0, failedFiles, scannedFiles);
    }

    public string ArchiveOldFiles(string folderLabel, string folderPath, string archiveRoot)
    {
        if (!Directory.Exists(folderPath))
        {
            return $"没有找到{folderLabel}。";
        }

        if (!TryPrepareArchiveRoot(archiveRoot, out var preparedArchiveRoot, out var archiveError))
        {
            return archiveError;
        }

        var cutoff = DateTime.UtcNow.Subtract(OldFileThreshold);
        var oldFiles = SafeEnumerateFiles(folderPath, MaxScanFilesPerRoot)
            .Select(ToFileInfoOrNull)
            .Where(info => info is not null && info.LastWriteTimeUtc < cutoff)
            .OrderByDescending(info => info!.Length)
            .Take(MaxArchiveCandidates)
            .ToList();

        if (oldFiles.Count == 0)
        {
            return $"{folderLabel}中未发现超过 3 个月未修改的文件。";
        }

        var result = MoveFilesToArchive(folderLabel, oldFiles!, preparedArchiveRoot);
        return BuildArchiveSummary(folderLabel, result);
    }

    public string ArchiveDesktopLargeFiles()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!Directory.Exists(desktop))
        {
            return "没有找到桌面目录。";
        }

        var archiveRoot = @"D:\PanGuardianData\DesktopArchive";
        if (!TryPrepareArchiveRoot(archiveRoot, out var preparedArchiveRoot, out var archiveError))
        {
            return archiveError;
        }

        var cutoff = DateTime.UtcNow.Subtract(OldFileThreshold);
        var candidates = SafeEnumerateFiles(desktop, MaxScanFilesPerRoot)
            .Select(ToFileInfoOrNull)
            .Where(info => info is not null && info.Length >= DesktopLargeFileThresholdBytes && info.LastWriteTimeUtc < cutoff)
            .OrderByDescending(info => info!.Length)
            .Take(20)
            .ToList();

        if (candidates.Count == 0)
        {
            return "桌面暂未发现超过 3 个月未修改且大于 200 MB 的文件。";
        }

        var result = MoveFilesToArchive("桌面", candidates!, preparedArchiveRoot);
        return BuildArchiveSummary("桌面大文件", result);
    }

    private static void AddCandidates(List<CleanupCandidateDto> results, string category, string folderPath, DateTime cutoff)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        foreach (var path in SafeEnumerateFiles(folderPath, MaxScanFilesPerRoot))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    results.Add(new CleanupCandidateDto
                    {
                        Category = category,
                        FileName = info.Name,
                        FullPath = info.FullName,
                        SizeBytes = info.Length,
                        LastModifiedAtUtc = info.LastWriteTimeUtc
                    });
                }
            }
            catch
            {
            }
        }
    }

    private static FileInfo? ToFileInfoOrNull(string path)
    {
        try
        {
            return new FileInfo(path);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryPrepareArchiveRoot(string archiveRoot, out string preparedArchiveRoot, out string errorMessage)
    {
        preparedArchiveRoot = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(archiveRoot))
        {
            errorMessage = "归档目录为空，已取消操作。";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(archiveRoot);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                errorMessage = "归档目录盘符无效，已取消操作。";
                return false;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                errorMessage = $"归档目标盘 {root} 不可用，已取消操作。";
                return false;
            }

            Directory.CreateDirectory(fullPath);
            preparedArchiveRoot = fullPath;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"无法准备归档目录：{ex.Message}";
            return false;
        }
    }

    private static ArchiveMoveResult MoveFilesToArchive(string folderLabel, IEnumerable<FileInfo?> files, string archiveRoot)
    {
        long movedBytes = 0;
        var movedFiles = new List<string>();
        var failedFiles = 0;
        var skippedFiles = 0;
        var datedFolder = Path.Combine(archiveRoot, DateTime.Now.ToString("yyyy-MM"));

        foreach (var info in files)
        {
            if (info is null)
            {
                skippedFiles++;
                continue;
            }

            try
            {
                if (!File.Exists(info.FullName))
                {
                    skippedFiles++;
                    continue;
                }

                Directory.CreateDirectory(datedFolder);
                var destination = EnsureUniquePath(Path.Combine(datedFolder, info.Name));
                var size = info.Length;
                File.Move(info.FullName, destination);
                movedBytes += size;
                movedFiles.Add($"{info.Name} -> {destination}");
            }
            catch
            {
                failedFiles++;
            }
        }

        return new ArchiveMoveResult(folderLabel, movedFiles, movedBytes, skippedFiles, failedFiles);
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{fileName}_{index}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string rootPath, int maxFiles)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || maxFiles <= 0)
        {
            yield break;
        }

        var yielded = 0;
        var scannedDirectories = 0;
        var directories = new Stack<string>();
        directories.Push(rootPath);

        while (directories.Count > 0 && yielded < maxFiles && scannedDirectories < MaxDirectoriesPerRoot)
        {
            var current = directories.Pop();
            scannedDirectories++;

            if (IsReparsePoint(current))
            {
                continue;
            }

            string[] files = [];
            string[] dirs = [];

            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
            }
            catch
            {
            }

            foreach (var file in files)
            {
                yield return file;
                yielded++;
                if (yielded >= maxFiles)
                {
                    yield break;
                }
            }

            try
            {
                dirs = Directory.EnumerateDirectories(current).ToArray();
            }
            catch
            {
            }

            foreach (var dir in dirs)
            {
                if (!IsReparsePoint(dir))
                {
                    directories.Push(dir);
                }
            }
        }
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

    private static void SendFileToRecycleBin(string path)
    {
        var operation = new ShFileOperation
        {
            wFunc = FileOperationType.Delete,
            pFrom = path + "\0\0",
            fFlags = FileOperationFlags.AllowUndo
                | FileOperationFlags.NoConfirmation
                | FileOperationFlags.NoErrorUi
                | FileOperationFlags.Silent
        };

        var result = SHFileOperation(ref operation);
        if (operation.fAnyOperationsAborted)
        {
            throw new OperationCanceledException("用户取消了文件清理操作。");
        }

        if (result != 0)
        {
            throw new IOException($"移动到回收站失败，系统返回码：{result}。");
        }
    }
    private static string BuildDeleteSummary(string actionName, int deletedFiles, long deletedBytes, int skippedFiles, int failedFiles, int? scannedFiles = null)
    {
        var prefix = scannedFiles is null
            ? $"已完成{actionName}，共处理 {deletedFiles} 个文件，释放约 {FormatBytes(deletedBytes)}。"
            : $"已完成{actionName}，扫描 {scannedFiles.Value} 个文件，成功处理 {deletedFiles} 个，释放约 {FormatBytes(deletedBytes)}。";

        var suffix = "文件已优先移动到回收站。";
        if (skippedFiles > 0 || failedFiles > 0)
        {
            suffix += $" 另有 {skippedFiles} 个文件已不存在，{failedFiles} 个文件因占用、权限或用户取消未处理。";
        }

        return prefix + suffix;
    }

    private static string BuildArchiveSummary(string folderLabel, ArchiveMoveResult result)
    {
        if (result.MovedFiles.Count == 0)
        {
            return $"没有成功归档{folderLabel}。{BuildFailureSuffix(result.SkippedFiles, result.FailedFiles)}";
        }

        var summary = $"已归档 {folderLabel} 中 {result.MovedFiles.Count} 个文件，合计约 {FormatBytes(result.MovedBytes)}。";
        var details = string.Join("\n", result.MovedFiles.Take(5));
        var suffix = BuildFailureSuffix(result.SkippedFiles, result.FailedFiles);
        return string.IsNullOrWhiteSpace(details)
            ? summary + suffix
            : summary + suffix + "\n" + details;
    }

    private static string BuildFailureSuffix(int skippedFiles, int failedFiles)
    {
        if (skippedFiles == 0 && failedFiles == 0)
        {
            return string.Empty;
        }

        return $" 另有 {skippedFiles} 个文件已不存在，{failedFiles} 个文件因占用或权限未处理。";
    }

    private static string FormatBytes(long bytes)
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


    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOperation fileOperation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOperation
    {
        public IntPtr hwnd;
        public FileOperationType wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public FileOperationFlags fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    private enum FileOperationType : uint
    {
        Delete = 0x0003
    }

    [Flags]
    private enum FileOperationFlags : ushort
    {
        Silent = 0x0004,
        NoConfirmation = 0x0010,
        AllowUndo = 0x0040,
        NoErrorUi = 0x0400
    }
    private sealed record ArchiveMoveResult(
        string FolderLabel,
        IReadOnlyList<string> MovedFiles,
        long MovedBytes,
        int SkippedFiles,
        int FailedFiles);
}
