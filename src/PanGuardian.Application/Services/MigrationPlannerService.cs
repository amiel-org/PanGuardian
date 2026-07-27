using System.IO;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationPlannerService : IMigrationPlanner
{
    public MigrationPlanDto BuildPlan()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        const string dDrive = @"D:\";

        var candidates = new List<MigrationCandidateDto>();
        if (!Directory.Exists(dDrive))
        {
            return new MigrationPlanDto { Candidates = candidates };
        }

        AddIfExists(candidates, "微信", "微信媒体与附件", Path.Combine(documents, "WeChat Files"), Path.Combine(dDrive, "PanGuardianData", "WeChat", "WeChat Files"), false, "通常包含图片、视频、附件，优先迁移。");
        AddIfExists(candidates, "微信", "微信本地缓存", Path.Combine(appDataLocal, "Tencent", "WeChat"), Path.Combine(dDrive, "PanGuardianData", "WeChat", "LocalCache"), false, "本地缓存和临时数据可迁移。");
        AddIfExists(candidates, "微信", "微信漫游 / 配置目录", Path.Combine(appDataRoaming, "Tencent", "WeChat"), Path.Combine(dDrive, "PanGuardianData", "WeChat", "Roaming"), true, "可能包含配置和记录，默认保护，不直接迁移。");
        AddIfExists(candidates, "企业微信", "企业微信工作数据", Path.Combine(appDataRoaming, "Tencent", "WXWork"), Path.Combine(dDrive, "PanGuardianData", "WeCom", "WXWork"), false, "通常包含工作附件和缓存，建议专项确认后迁移。");
        AddIfExists(candidates, "企业微信", "企业微信本地缓存", Path.Combine(appDataLocal, "Tencent", "WXWork"), Path.Combine(dDrive, "PanGuardianData", "WeCom", "LocalCache"), false, "本地缓存与临时文件优先迁移。");
        AddIfExists(candidates, "企业微信", "企业微信文档目录", Path.Combine(documents, "WXWork"), Path.Combine(dDrive, "PanGuardianData", "WeCom", "Documents"), true, "可能混有业务文件，默认保护，不直接迁移。");

        return new MigrationPlanDto
        {
            Candidates = candidates
                .OrderBy(item => item.IsProtected)
                .ThenByDescending(item => item.SizeBytes)
                .ToList()
        };
    }

    private static void AddIfExists(
        List<MigrationCandidateDto> candidates,
        string appName,
        string displayName,
        string sourcePath,
        string targetPath,
        bool isProtected,
        string reason)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        candidates.Add(new MigrationCandidateDto
        {
            AppName = appName,
            DisplayName = displayName,
            SourcePath = sourcePath,
            SuggestedTargetPath = targetPath,
            SizeBytes = MeasureDirectory(sourcePath),
            IsProtected = isProtected,
            Reason = reason
        });
    }

    private static long MeasureDirectory(string rootPath)
    {
        long sizeBytes = 0;
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

                        sizeBytes += new FileInfo(file).Length;
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

        return sizeBytes;
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
}
