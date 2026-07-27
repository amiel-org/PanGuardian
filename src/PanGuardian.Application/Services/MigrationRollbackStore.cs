using System.IO;
using System.Text.Json;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationRollbackStore : IMigrationRollbackStore
{
    private readonly string _recordFilePath;

    public MigrationRollbackStore()
    {
        _recordFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migration-rollback-points.jsonl");
    }

    public void Append(MigrationRollbackPointDto point)
    {
        try
        {
            var directory = Path.GetDirectoryName(_recordFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(point);
            File.AppendAllText(_recordFilePath, json + Environment.NewLine);
        }
        catch
        {
        }
    }
}
