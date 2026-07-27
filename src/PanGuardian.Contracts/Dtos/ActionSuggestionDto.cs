namespace PanGuardian.Contracts.Dtos;

public sealed class ActionSuggestionDto
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required string PrimaryActionLabel { get; init; }
}
