namespace CodeUsageMap.Vsix.ViewModels
{

internal sealed class UsageMapMiniMapNodeItemViewModel
{
    public required string Id { get; init; }

    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public string Fill { get; init; } = "#FFCBD5E0";

    public double Opacity { get; init; } = 0.8d;
}
}
