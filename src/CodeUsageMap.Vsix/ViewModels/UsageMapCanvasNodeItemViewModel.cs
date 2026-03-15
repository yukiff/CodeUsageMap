using System.Collections.Generic;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;

namespace CodeUsageMap.Vsix.ViewModels;

internal sealed class UsageMapCanvasNodeItemViewModel : ViewModelBase
{
    private bool _isSelected;
    private bool _isCollapsed;

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string SymbolKey { get; init; } = string.Empty;

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public bool IsRoot { get; init; }

    public bool IsExternal { get; init; }

    public string ExternalCategory { get; init; } = string.Empty;

    public CanvasNodeLane Lane { get; init; } = CanvasNodeLane.Related;

    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; } = 220d;

    public double Height { get; init; } = 72d;

    public string Fill { get; init; } = "#FFF7F7F7";

    public string BorderBrush { get; init; } = "#FFB8B8B8";

    public string KindLabel { get; init; } = "?";

    public double Opacity { get; set; } = 1d;

    public IReadOnlyList<UsageMapDetailItemViewModel> Details { get; init; } = [];

    public bool HasChildren { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(BorderThickness));
            }
        }
    }

    public double BorderThickness => IsSelected ? 3d : 1d;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetProperty(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(CollapseButtonLabel));
            }
        }
    }

    public string CollapseButtonLabel => IsCollapsed ? "Expand" : "Collapse";
}
