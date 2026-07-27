namespace PanGuardian.Contracts.Dtos;

public sealed class DashboardSnapshotDto
{
    public required string DriveName { get; init; }
    public long TotalBytes { get; init; }
    public long UsedBytes { get; init; }
    public long FreeBytes { get; init; }
    public long GrowthInLast7DaysBytes { get; init; }
    public IReadOnlyList<SourceUsageDto> TopSources { get; init; } = [];
    public IReadOnlyList<SourceDetailDto> SourceDetails { get; init; } = [];
    public IReadOnlyList<LargeFileDto> LargeFiles { get; init; } = [];
    public IReadOnlyList<ActionSuggestionDto> Suggestions { get; init; } = [];
}
