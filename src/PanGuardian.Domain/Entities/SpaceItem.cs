using PanGuardian.Domain.Enums;

namespace PanGuardian.Domain.Entities;

public sealed class SpaceItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string SourceName { get; init; }
    public long SizeBytes { get; init; }
    public long GrowthBytes { get; init; }
    public RiskLevel RiskLevel { get; init; }
    public RecommendedAction RecommendedAction { get; init; }
    public string Description { get; init; } = string.Empty;
}
