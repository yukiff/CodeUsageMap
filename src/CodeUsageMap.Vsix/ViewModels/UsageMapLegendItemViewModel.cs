namespace CodeUsageMap.Vsix.ViewModels;

internal sealed class UsageMapLegendItemViewModel
{
    public required string Swatch { get; init; }

    public required string Label { get; init; }

    public string Description { get; init; } = string.Empty;
}
