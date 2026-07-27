namespace PanGuardian.Domain.Entities;

public sealed class DriveSummary
{
    public required string DriveName { get; init; }
    public long TotalBytes { get; init; }
    public long UsedBytes { get; init; }
    public long FreeBytes { get; init; }
    public long GrowthInLast7DaysBytes { get; init; }
    public double UsedPercentage => TotalBytes == 0 ? 0 : (double)UsedBytes / TotalBytes * 100;
}
