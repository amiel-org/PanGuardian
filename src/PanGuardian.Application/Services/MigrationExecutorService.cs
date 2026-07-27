using System.IO;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationExecutorService : IMigrationExecutor
{
    private readonly IMigrationRecordStore _recordStore;
    private readonly IMigrationRollbackStore _rollbackStore;
    private readonly IMigrationPrechecker _prechecker;

    public MigrationExecutorService(
        IMigrationRecordStore recordStore,
        IMigrationRollbackStore rollbackStore,
        IMigrationPrechecker prechecker)
    {
        _recordStore = recordStore;
        _rollbackStore = rollbackStore;
        _prechecker = prechecker;
    }

    public MigrationExecutionResultDto ExecuteCopy(MigrationCandidateDto candidate)
    {
        MigrationExecutionResultDto result;

        try
        {
            if (candidate.IsProtected)
            {
                result = new MigrationExecutionResultDto
                {
                    SourcePath = candidate.SourcePath,
                    TargetPath = candidate.SuggestedTargetPath,
                    Success = false,
                    Message = "该目录被标记为受保护，当前版本不会直接迁移。",
                    SourceFileCount = 0,
                    TargetFileCount = 0,
                    SourceBytes = 0,
                    TargetBytes = 0
                };
                SaveRecord(candidate, result);
                return result;
            }

            var precheck = _prechecker.Check(candidate);
            if (!precheck.CanProceed)
            {
                result = new MigrationExecutionResultDto
                {
                    SourcePath = precheck.SourcePath,
                    TargetPath = precheck.TargetPath,
                    Success = false,
                    Message = precheck.Message,
                    SourceFileCount = 0,
                    TargetFileCount = 0,
                    SourceBytes = precheck.RequiredBytes,
                    TargetBytes = precheck.AvailableBytes
                };
                SaveRecord(candidate, result);
                return result;
            }

            Directory.CreateDirectory(precheck.TargetPath);
            CopyDirectory(precheck.SourcePath, precheck.TargetPath);

            var sourceSnapshot = MeasureDirectory(precheck.SourcePath);
            var targetSnapshot = MeasureDirectory(precheck.TargetPath);
            var verified = sourceSnapshot.FileCount == targetSnapshot.FileCount
                && sourceSnapshot.TotalBytes == targetSnapshot.TotalBytes;

            if (verified)
            {
                _rollbackStore.Append(new MigrationRollbackPointDto
                {
                    AppName = candidate.AppName,
                    SourcePath = precheck.SourcePath,
                    TargetPath = precheck.TargetPath,
                    BackupHintPath = precheck.SourcePath + ".pangd.backup",
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            result = new MigrationExecutionResultDto
            {
                SourcePath = precheck.SourcePath,
                TargetPath = precheck.TargetPath,
                Success = verified,
                Message = verified
                    ? "已完成安全复制并通过基础校验，同时已记录回滚点信息。"
                    : "复制已完成，但源目录与目标目录的文件数或大小不一致，需要人工复核。",
                SourceFileCount = sourceSnapshot.FileCount,
                TargetFileCount = targetSnapshot.FileCount,
                SourceBytes = sourceSnapshot.TotalBytes,
                TargetBytes = targetSnapshot.TotalBytes
            };
        }
        catch (Exception ex)
        {
            result = new MigrationExecutionResultDto
            {
                SourcePath = candidate.SourcePath,
                TargetPath = candidate.SuggestedTargetPath,
                Success = false,
                Message = $"复制失败：{ex.Message}",
                SourceFileCount = 0,
                TargetFileCount = 0,
                SourceBytes = 0,
                TargetBytes = 0
            };
        }

        SaveRecord(candidate, result);
        return result;
    }

    private void SaveRecord(MigrationCandidateDto candidate, MigrationExecutionResultDto result)
    {
        _recordStore.Append(new MigrationRecordEntry
        {
            AppName = candidate.AppName,
            DisplayName = candidate.DisplayName,
            SourcePath = result.SourcePath,
            TargetPath = result.TargetPath,
            Message = result.Message,
            Success = result.Success,
            SourceFileCount = result.SourceFileCount,
            TargetFileCount = result.TargetFileCount,
            SourceBytes = result.SourceBytes,
            TargetBytes = result.TargetBytes,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static void CopyDirectory(string sourcePath, string targetPath)
    {
        var directories = new Stack<string>();
        directories.Push(sourcePath);

        while (directories.Count > 0)
        {
            var current = directories.Pop();
            if (IsReparsePoint(current))
            {
                continue;
            }

            var relativeDirectory = Path.GetRelativePath(sourcePath, current);
            var targetDirectory = relativeDirectory == "."
                ? targetPath
                : Path.Combine(targetPath, relativeDirectory);
            Directory.CreateDirectory(targetDirectory);

            string[] files;
            string[] childDirectories;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
                childDirectories = Directory.EnumerateDirectories(current).ToArray();
            }
            catch
            {
                throw new IOException($"无法枚举源目录：{current}");
            }

            foreach (var file in files)
            {
                if (IsReparsePoint(file))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourcePath, file);
                var destination = Path.Combine(targetPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }

            foreach (var directory in childDirectories)
            {
                if (!IsReparsePoint(directory))
                {
                    directories.Push(directory);
                }
            }
        }
    }

    private static DirectorySnapshot MeasureDirectory(string rootPath)
    {
        long fileCount = 0;
        long totalBytes = 0;
        var directories = new Stack<string>();
        directories.Push(rootPath);

        while (directories.Count > 0)
        {
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
                    try
                    {
                        if (IsReparsePoint(file))
                        {
                            continue;
                        }

                        var info = new FileInfo(file);
                        fileCount++;
                        totalBytes += info.Length;
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
                    if (!IsReparsePoint(directory))
                    {
                        directories.Push(directory);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return new DirectorySnapshot(fileCount, totalBytes);
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

    private sealed record DirectorySnapshot(long FileCount, long TotalBytes);
}
