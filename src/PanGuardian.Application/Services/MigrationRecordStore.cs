using System.IO;
using System.Text.Json;
using PanGuardian.Contracts.Dtos;
using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class MigrationRecordStore : IMigrationRecordStore
{
    private readonly string _recordFilePath;

    public MigrationRecordStore()
    {
        _recordFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migration-records.jsonl");
    }

    public void Append(MigrationRecordEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(_recordFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(entry);
            File.AppendAllText(_recordFilePath, json + Environment.NewLine);
        }
        catch
        {
        }
    }
}
