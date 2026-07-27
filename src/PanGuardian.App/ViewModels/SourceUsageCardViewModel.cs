namespace PanGuardian.App.ViewModels;

public sealed class SourceUsageCardViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Badge { get; init; } = string.Empty;
    public string SizeText { get; init; } = string.Empty;
    public double SizePercent { get; init; }
    public string SizePercentText { get; init; } = string.Empty;
    public string GrowthText { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string PathHint { get; init; } = string.Empty;
}
