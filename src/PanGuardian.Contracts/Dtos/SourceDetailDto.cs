namespace PanGuardian.Contracts.Dtos;

public sealed class SourceDetailDto
{
    public required string SourceName { get; init; }
    public required string Path { get; init; }
    public long SizeBytes { get; init; }
    public long GrowthBytes { get; init; }
}
