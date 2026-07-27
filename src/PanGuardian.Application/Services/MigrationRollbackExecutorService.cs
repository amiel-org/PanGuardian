using System.IO;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationRollbackExecutorService : IMigrationRollbackExecutor
{
    public MigrationRollbackResultDto Restore(MigrationCandidateDto candidate)
    {
        var backupPath = candidate.SourcePath + ".pangd.backup";

        try
        {
            if (!Directory.Exists(backupPath))
            {
                return new MigrationRollbackResultDto
                {
                    SourcePath = candidate.SourcePath,
                    BackupPath = backupPath,
                    Success = false,
                    Message = "没有找到可恢复的备份目录。"
                };
            }

            if (Directory.Exists(candidate.SourcePath))
            {
                var attributes = File.GetAttributes(candidate.SourcePath);
                var isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);

                if (isReparsePoint)
                {
                    Directory.Delete(candidate.SourcePath);
                }
                else
                {
                    return new MigrationRollbackResultDto
                    {
                        SourcePath = candidate.SourcePath,
                        BackupPath = backupPath,
                        Success = false,
                        Message = "当前原路径不是联接目录，无法自动回滚。"
                    };
                }
            }

            Directory.Move(backupPath, candidate.SourcePath);

            return new MigrationRollbackResultDto
            {
                SourcePath = candidate.SourcePath,
                BackupPath = backupPath,
                Success = true,
                Message = "已恢复原目录。"
            };
        }
        catch (Exception ex)
        {
            return new MigrationRollbackResultDto
            {
                SourcePath = candidate.SourcePath,
                BackupPath = backupPath,
                Success = false,
                Message = $"回滚失败：{ex.Message}"
            };
        }
    }
}
