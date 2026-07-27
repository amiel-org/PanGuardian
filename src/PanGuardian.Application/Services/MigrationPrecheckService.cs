using System.Diagnostics;
using System.IO;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationPrecheckService : IMigrationPrechecker
{
    public MigrationPrecheckResultDto Check(MigrationCandidateDto candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.SourcePath) || string.IsNullOrWhiteSpace(candidate.SuggestedTargetPath))
        {
            return BuildFailure(candidate, "源路径或目标路径为空，无法执行预检查。", 0, 0, false, false, true);
        }

        string sourcePath;
        string targetPath;
        try
        {
            sourcePath = Path.GetFullPath(candidate.SourcePath);
            targetPath = Path.GetFullPath(candidate.SuggestedTargetPath);
        }
        catch (Exception ex)
        {
            return BuildFailure(candidate, $"路径格式无效：{ex.Message}", 0, 0, false, false, true);
        }

        if (PathsEqual(sourcePath, targetPath))
        {
            return BuildFailure(candidate, "源路径与目标路径相同，当前不会执行复制。", candidate.SizeBytes, 0, false, false, true);
        }

        if (IsNestedPath(sourcePath, targetPath) || IsNestedPath(targetPath, sourcePath))
        {
            return BuildFailure(candidate, "源路径与目标路径存在包含关系，当前不会执行复制，以免形成递归拷贝。", candidate.SizeBytes, 0, false, false, true);
        }

        if (!Directory.Exists(sourcePath))
        {
            return BuildFailure(candidate, "源目录不存在。", 0, 0, false, false, true);
        }

        if (IsReparsePoint(sourcePath))
        {
            return BuildFailure(candidate, "当前原路径已经是联接目录，无需再次复制。", candidate.SizeBytes, 0, false, false, true);
        }

        var requiredBytes = Math.Max(candidate.SizeBytes, 0);
        var targetRoot = Path.GetPathRoot(targetPath) ?? @"C:\";

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(targetRoot);
        }
        catch (Exception ex)
        {
            return BuildFailure(candidate, $"无法访问目标盘：{ex.Message}", requiredBytes, 0, false, false, true);
        }

        long availableBytes;
        try
        {
            availableBytes = drive.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            return BuildFailure(candidate, $"无法读取目标盘剩余空间：{ex.Message}", requiredBytes, 0, false, false, true);
        }

        var hasEnoughSpace = availableBytes >= requiredBytes;

        bool targetPathAccessible;
        var targetIsEmptyOrMissing = CheckTargetIsEmptyOrMissing(targetPath, out targetPathAccessible);
        if (!targetPathAccessible)
        {
            return BuildFailure(candidate, "目标路径无法访问或无法枚举，当前不会自动覆盖。", requiredBytes, availableBytes, hasEnoughSpace, false, true);
        }

        var isAppClosed = IsApplicationClosed(candidate.AppName);
        var canProceed = !candidate.IsProtected && hasEnoughSpace && targetIsEmptyOrMissing && isAppClosed;
        var message = canProceed
            ? "预检查通过，可以执行安全复制。"
            : BuildMessage(candidate, hasEnoughSpace, targetIsEmptyOrMissing, isAppClosed, requiredBytes, availableBytes);

        return new MigrationPrecheckResultDto
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CanProceed = canProceed,
            HasEnoughSpace = hasEnoughSpace,
            TargetIsEmptyOrMissing = targetIsEmptyOrMissing,
            IsAppClosed = isAppClosed,
            Message = message,
            RequiredBytes = requiredBytes,
            AvailableBytes = availableBytes
        };
    }

    private static MigrationPrecheckResultDto BuildFailure(
        MigrationCandidateDto candidate,
        string message,
        long requiredBytes,
        long availableBytes,
        bool hasEnoughSpace,
        bool targetIsEmptyOrMissing,
        bool isAppClosed)
    {
        return new MigrationPrecheckResultDto
        {
            SourcePath = candidate.SourcePath,
            TargetPath = candidate.SuggestedTargetPath,
            CanProceed = false,
            HasEnoughSpace = hasEnoughSpace,
            TargetIsEmptyOrMissing = targetIsEmptyOrMissing,
            IsAppClosed = isAppClosed,
            Message = message,
            RequiredBytes = requiredBytes,
            AvailableBytes = availableBytes
        };
    }

    private static bool CheckTargetIsEmptyOrMissing(string targetPath, out bool accessible)
    {
        accessible = true;
        if (!Directory.Exists(targetPath))
        {
            return true;
        }

        try
        {
            return !Directory.EnumerateFileSystemEntries(targetPath).Any();
        }
        catch
        {
            accessible = false;
            return false;
        }
    }

    private static bool IsApplicationClosed(string appName)
    {
        var processNames = appName switch
        {
            "微信" => new[] { "WeChat", "Weixin" },
            "企业微信" => new[] { "WXWork", "WXWorkWX", "WeCom" },
            _ => Array.Empty<string>()
        };

        return processNames.All(name => !Process.GetProcessesByName(name).Any());
    }

    private static string BuildMessage(
        MigrationCandidateDto candidate,
        bool hasEnoughSpace,
        bool targetIsEmptyOrMissing,
        bool isAppClosed,
        long requiredBytes,
        long availableBytes)
    {
        if (candidate.IsProtected)
        {
            return "该目录被标记为受保护，当前版本不会直接迁移。";
        }

        if (!isAppClosed)
        {
            return "检测到微信或企业微信仍在运行，请先退出应用再执行迁移。";
        }

        if (!hasEnoughSpace)
        {
            return $"目标盘空间不足，预计至少需要 {FormatBytes(requiredBytes)}，当前可用 {FormatBytes(availableBytes)}。";
        }

        if (!targetIsEmptyOrMissing)
        {
            return "目标路径已存在内容，当前版本不会覆盖到非空迁移目录。";
        }

        return "预检查未通过。";
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNestedPath(string parentPath, string candidatePath)
    {
        var normalizedParent = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
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
}
