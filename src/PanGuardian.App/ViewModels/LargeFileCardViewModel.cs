namespace PanGuardian.App.ViewModels;

public sealed class LargeFileCardViewModel
{
    public string FileName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string PathText { get; init; } = string.Empty;
    public string SizeText { get; init; } = string.Empty;
    public string ModifiedText { get; init; } = string.Empty;
}
