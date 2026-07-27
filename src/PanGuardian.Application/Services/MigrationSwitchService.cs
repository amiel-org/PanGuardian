using System.Diagnostics;
using System.IO;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationSwitchService : IMigrationSwitcher
{
    public MigrationSwitchResultDto SwitchToJunction(MigrationCandidateDto candidate)
    {
        var sourcePath = candidate.SourcePath;
        var targetPath = candidate.SuggestedTargetPath;
        var backupPath = candidate.SourcePath + ".pangd.backup";

        try
        {
            if (candidate.IsProtected)
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "该目录被标记为受保护，当前版本不会自动切换联接。");
            }

            if (string.IsNullOrWhiteSpace(candidate.SourcePath) || string.IsNullOrWhiteSpace(candidate.SuggestedTargetPath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "源路径或目标路径为空，无法切换联接。");
            }

            sourcePath = Path.GetFullPath(candidate.SourcePath);
            targetPath = Path.GetFullPath(candidate.SuggestedTargetPath);
            backupPath = sourcePath + ".pangd.backup";

            if (PathsEqual(sourcePath, targetPath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "源路径与目标路径相同，无法切换联接。");
            }

            if (IsNestedPath(sourcePath, targetPath) || IsNestedPath(targetPath, sourcePath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "源路径与目标路径存在包含关系，无法切换联接，以免形成递归访问。");
            }

            if (!Directory.Exists(sourcePath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "源目录不存在，无法切换到联接。");
            }

            if (IsReparsePoint(sourcePath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "当前源目录已经是联接或重解析点，无需重复切换。");
            }

            if (!Directory.Exists(targetPath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "目标目录不存在，需先完成复制迁移。");
            }

            if (!SafeHasEntries(targetPath, out var targetAccessible))
            {
                var message = targetAccessible
                    ? "目标目录为空，当前不会切换到空目录联接。"
                    : "目标目录无法访问或无法枚举，当前不会切换联接。";
                return BuildResult(sourcePath, targetPath, backupPath, false, message);
            }

            if (Directory.Exists(backupPath) || File.Exists(backupPath))
            {
                return BuildResult(sourcePath, targetPath, backupPath, false, "已存在旧的备份路径，请先人工确认后再切换。");
            }

            Directory.Move(sourcePath, backupPath);

            try
            {
                CreateJunction(sourcePath, targetPath);
                return BuildResult(sourcePath, targetPath, backupPath, true, "目录联接已创建，原目录已重命名为备份目录。");
            }
            catch (Exception ex)
            {
                var restoreMessage = RestoreAfterFailedSwitch(sourcePath, backupPath);
                return BuildResult(sourcePath, targetPath, backupPath, false, $"联接创建失败：{ex.Message}{restoreMessage}");
            }
        }
        catch (Exception ex)
        {
            return BuildResult(sourcePath, targetPath, backupPath, false, $"联接切换失败：{ex.Message}");
        }
    }

    private static string RestoreAfterFailedSwitch(string sourcePath, string backupPath)
    {
        try
        {
            if (Directory.Exists(sourcePath))
            {
                if (IsReparsePoint(sourcePath))
                {
                    Directory.Delete(sourcePath);
                }
                else
                {
                    return "；已保留现场：原路径位置出现非联接目录，未自动删除，请人工检查备份目录。";
                }
            }

            if (Directory.Exists(backupPath) && !Directory.Exists(sourcePath))
            {
                Directory.Move(backupPath, sourcePath);
                return "；已自动恢复原目录。";
            }
        }
        catch (Exception restoreEx)
        {
            return $"；自动恢复原目录也失败：{restoreEx.Message}";
        }

        return string.Empty;
    }

    private static MigrationSwitchResultDto BuildResult(string sourcePath, string targetPath, string backupPath, bool success, string message)
    {
        return new MigrationSwitchResultDto
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            BackupPath = backupPath,
            Success = success,
            Message = message
        };
    }

    private static bool SafeHasEntries(string path, out bool accessible)
    {
        accessible = true;
        try
        {
            return Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            accessible = false;
            return false;
        }
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

    private static void CreateJunction(string linkPath, string targetPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 mklink 进程。");

        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        process.WaitForExit();
        var error = errorTask.GetAwaiter().GetResult();
        var output = outputTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        }
    }
}
