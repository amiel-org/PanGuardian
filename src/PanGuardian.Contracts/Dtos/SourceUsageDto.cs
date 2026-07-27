namespace PanGuardian.Contracts.Dtos;

public sealed class SourceUsageDto
{
    public required string Name { get; init; }
    public long SizeBytes { get; init; }
    public long GrowthBytes { get; init; }
    public required string Badge { get; init; }
    public string? PrimaryPath { get; init; }
}
