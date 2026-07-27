using PanGuardian.Domain.Interfaces;

namespace PanGuardian.Application.Services;

public sealed class ByteSizeFormatter : IStorageFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {Units[unitIndex]}";
    }
}
