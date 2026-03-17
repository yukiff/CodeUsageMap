using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Contracts.Presentation;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Vsix.Services;
using PresentationGraphCanvasViewModel = CodeUsageMap.Contracts.Presentation.GraphCanvasViewModel;
using PresentationUsageMapViewModel = CodeUsageMap.Contracts.Presentation.UsageMapViewModel;

namespace CodeUsageMap.Vsix.ViewModels
{

internal sealed class UsageMapViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private readonly UsageMapViewModelFilter _viewModelFilter = new();
    private readonly UsageNodeAssessmentBuilder _nodeAssessmentBuilder = new();
    private readonly HashSet<string> _collapsedCanvasNodeIds = new(StringComparer.Ordinal);
    private UsageMapNodeItemViewModel? _rootNode;
    private UsageMapNodeItemViewModel? _selectedNode;
    private UsageMapCanvasNodeItemViewModel? _selectedCanvasNode;
    private UsageMapRelationItemViewModel? _selectedRelation;
    private PresentationUsageMapViewModel? _loadedModel;
    private PresentationUsageMapViewModel? _activeModel;
    private PresentationGraphCanvasViewModel? _loadedCanvasModel;
    private string _title = "Usage Map";
    private string _summary = string.Empty;
    private string _statusMessage = "Ready.";
    private string _errorMessage = string.Empty;
    private string _rootSearchText = string.Empty;
    private string _rootSearchStatusMessage = "Type 2 or more characters to search for a new root.";
    private string _searchText = string.Empty;
    private string _selectedEdgeKind = AllFilterValue;
    private string _selectedNodeKind = AllFilterValue;
    private string _selectedProjectName = AllFilterValue;
    private string _selectedNamespaceName = AllFilterValue;
    private string _selectedAccessibility = AllFilterValue;
    private string _selectedMinimumConfidence = AllFilterValue;
    private GraphCanvasDisplayMode _selectedDisplayMode = GraphCanvasDisplayMode.CallMap;
    private bool _isBusy;
    private int _selectedDepth = 1;
    private bool _excludeTests;
    private bool _excludeGenerated;
    private bool _excludeExternalSymbols;
    private bool _excludeSystemSymbols;
    private bool _excludePackageSymbols;
    private bool _highlightInterfaceFlow;
    private bool _highlightEventFlow;
    private bool _highlightAsyncFlow;
    private int _selectedCanvasNodeLimit = 40;
    private bool _canvasFocusMode;
    private bool _followCaret;
    private bool _hasSymbolCandidates;
    private bool _hasExportableResult;
    private double _miniMapWidth = 180d;
    private double _miniMapHeight = 120d;
    private double _miniMapViewportLeft;
    private double _miniMapViewportTop;
    private double _miniMapViewportWidth;
    private double _miniMapViewportHeight;
    private double _graphCanvasWidth = 960d;
    private double _graphCanvasHeight = 180d;
    private string _selectedNodeTitle = "No node selected";
    private string _selectedNodeSubtitle = string.Empty;
    private string _selectedNodeProject = string.Empty;
    private string _selectedNodeLocation = string.Empty;
    private string _selectedNodeSignature = string.Empty;
    private string _selectedNodeSummaryText = string.Empty;
    private UsageMapSymbolCandidateItemViewModel? _selectedSymbolCandidate;
    private UsageMapRootSearchResultItemViewModel? _selectedRootSearchResult;
    private CancellationTokenSource? _analysisCancellation;
    private CancellationTokenSource? _rootSearchCancellation;
    private Func<UsageMapExportFormat, Task>? _exportHandler;
    private Func<string, CancellationToken, Task<IReadOnlyList<UsageMapRootSearchResultItemViewModel>>>? _rootSearchHandler;
    private Func<UsageMapRootSearchResultItemViewModel, Task>? _applyRootSearchResultHandler;
    private Func<AnalyzeOptions, Task>? _refreshHandler;
    private Func<bool, Task>? _followCaretHandler;
    private Func<UsageMapCanvasNodeItemViewModel, Task>? _rerootHandler;
    private const string AllFilterValue = "All";

    public UsageMapViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        OpenSelectedNodeCommand = new RelayCommand(OpenSelectedNode, CanOpenSelectedNode);
        OpenSelectedRelationCommand = new RelayCommand(OpenSelectedRelation, CanOpenSelectedRelation);
        RefreshAnalysisCommand = new RelayCommand(RefreshAnalysis, CanRefreshAnalysis);
        CancelAnalysisCommand = new RelayCommand(CancelAnalysis, CanCancelAnalysis);
        ClearFiltersCommand = new RelayCommand(ClearFilters, CanClearFilters);
        ApplySelectedRootSearchResultCommand = new RelayCommand(ApplySelectedRootSearchResult, CanApplySelectedRootSearchResult);
        ClearRootSearchCommand = new RelayCommand(ClearRootSearch, CanClearRootSearch);
        AnalyzeSelectedCandidateCommand = new RelayCommand(AnalyzeSelectedCandidate, CanAnalyzeSelectedCandidate);
        ExportJsonCommand = new RelayCommand(ExportJson, CanExport);
        ExportViewModelJsonCommand = new RelayCommand(ExportViewModelJson, CanExport);
        ExportDgmlCommand = new RelayCommand(ExportDgml, CanExport);
        AvailableProjectNames.Add(AllFilterValue);
        AvailableNamespaceNames.Add(AllFilterValue);
        AvailableAccessibilities.Add(AllFilterValue);
        UpdateLegendItems();
    }

    public ObservableCollection<UsageMapNodeItemViewModel> Nodes { get; } = new ObservableCollection<UsageMapNodeItemViewModel>();

    public ObservableCollection<UsageMapRelationItemViewModel> IncomingRelations { get; } = new ObservableCollection<UsageMapRelationItemViewModel>();

    public ObservableCollection<UsageMapRelationItemViewModel> OutgoingRelations { get; } = new ObservableCollection<UsageMapRelationItemViewModel>();

    public ObservableCollection<UsageMapRelationItemViewModel> RelatedRelations { get; } = new ObservableCollection<UsageMapRelationItemViewModel>();

    public ObservableCollection<UsageMapDetailItemViewModel> Details { get; } = new ObservableCollection<UsageMapDetailItemViewModel>();

    public ObservableCollection<UsageMapDiagnosticItemViewModel> Diagnostics { get; } = new ObservableCollection<UsageMapDiagnosticItemViewModel>();

    public ObservableCollection<string> AvailableProjectNames { get; } = new ObservableCollection<string>();

    public ObservableCollection<string> AvailableNamespaceNames { get; } = new ObservableCollection<string>();

    public ObservableCollection<string> AvailableAccessibilities { get; } = new ObservableCollection<string>();

    public ObservableCollection<UsageMapSymbolCandidateItemViewModel> SymbolCandidates { get; } = new ObservableCollection<UsageMapSymbolCandidateItemViewModel>();

    public ObservableCollection<UsageMapCanvasNodeItemViewModel> GraphCanvasNodes { get; } = new ObservableCollection<UsageMapCanvasNodeItemViewModel>();

    public ObservableCollection<UsageMapCanvasEdgeItemViewModel> GraphCanvasEdges { get; } = new ObservableCollection<UsageMapCanvasEdgeItemViewModel>();

    public ObservableCollection<UsageMapRootSearchResultItemViewModel> RootSearchResults { get; } = new ObservableCollection<UsageMapRootSearchResultItemViewModel>();

    public ObservableCollection<UsageMapBreadcrumbItemViewModel> BreadcrumbItems { get; } = new ObservableCollection<UsageMapBreadcrumbItemViewModel>();

    public ObservableCollection<UsageMapLegendItemViewModel> LegendItems { get; } = new ObservableCollection<UsageMapLegendItemViewModel>();

    public ObservableCollection<UsageMapMiniMapNodeItemViewModel> MiniMapNodes { get; } = new ObservableCollection<UsageMapMiniMapNodeItemViewModel>();

    public ObservableCollection<UsageMapStatItemViewModel> SelectedNodeStats { get; } = new ObservableCollection<UsageMapStatItemViewModel>();

    public ObservableCollection<UsageMapStatItemViewModel> SelectedNodeImpactSummary { get; } = new ObservableCollection<UsageMapStatItemViewModel>();

    public ObservableCollection<UsageMapStatItemViewModel> SelectedNodeRiskSummary { get; } = new ObservableCollection<UsageMapStatItemViewModel>();

    public ObservableCollection<UsageMapDetailItemViewModel> SelectedNodeHighlights { get; } = new ObservableCollection<UsageMapDetailItemViewModel>();

    public RelayCommand OpenSelectedNodeCommand { get; }

    public RelayCommand OpenSelectedRelationCommand { get; }

    public RelayCommand RefreshAnalysisCommand { get; }

    public RelayCommand CancelAnalysisCommand { get; }

    public RelayCommand ClearFiltersCommand { get; }

    public RelayCommand ApplySelectedRootSearchResultCommand { get; }

    public RelayCommand ClearRootSearchCommand { get; }

    public RelayCommand AnalyzeSelectedCandidateCommand { get; }

    public RelayCommand ExportJsonCommand { get; }

    public RelayCommand ExportViewModelJsonCommand { get; }

    public RelayCommand ExportDgmlCommand { get; }

    public IReadOnlyList<int> AvailableDepths { get; } = new[] { 1, 2, 3 };

    public IReadOnlyList<string> AvailableEdgeKinds { get; } =
        new[] { AllFilterValue }.Concat(Enum.GetNames(typeof(EdgeKind)).Cast<string>()).ToArray();

    public IReadOnlyList<string> AvailableNodeKinds { get; } =
        new[] { AllFilterValue }.Concat(Enum.GetNames(typeof(NodeKind)).Cast<string>()).ToArray();

    public IReadOnlyList<string> AvailableConfidenceThresholds { get; } =
        new[] { AllFilterValue, "0.50", "0.75", "0.90", "1.00" };

    public IReadOnlyList<GraphCanvasDisplayMode> AvailableDisplayModes { get; } =
        Enum.GetValues(typeof(GraphCanvasDisplayMode)).Cast<GraphCanvasDisplayMode>().ToArray();

    public IReadOnlyList<int> AvailableCanvasNodeLimits { get; } = new[] { 24, 40, 80, 160 };

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public string RootSearchText
    {
        get => _rootSearchText;
        set
        {
            if (SetProperty(ref _rootSearchText, value))
            {
                ScheduleRootSearch();
                ClearRootSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RootSearchStatusMessage
    {
        get => _rootSearchStatusMessage;
        private set => SetProperty(ref _rootSearchStatusMessage, value);
    }

    public string SelectedNamespaceName
    {
        get => _selectedNamespaceName;
        set
        {
            if (SetProperty(ref _selectedNamespaceName, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedAccessibility
    {
        get => _selectedAccessibility;
        set
        {
            if (SetProperty(ref _selectedAccessibility, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasRootSearchResults => RootSearchResults.Count > 0;

    public bool ExcludeExternalSymbols
    {
        get => _excludeExternalSymbols;
        set
        {
            if (SetProperty(ref _excludeExternalSymbols, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ExcludeSystemSymbols
    {
        get => _excludeSystemSymbols;
        set
        {
            if (SetProperty(ref _excludeSystemSymbols, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ExcludePackageSymbols
    {
        get => _excludePackageSymbols;
        set
        {
            if (SetProperty(ref _excludePackageSymbols, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int SelectedCanvasNodeLimit
    {
        get => _selectedCanvasNodeLimit;
        set
        {
            if (SetProperty(ref _selectedCanvasNodeLimit, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanvasFocusMode
    {
        get => _canvasFocusMode;
        set
        {
            if (SetProperty(ref _canvasFocusMode, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HighlightInterfaceFlow
    {
        get => _highlightInterfaceFlow;
        set
        {
            if (SetProperty(ref _highlightInterfaceFlow, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HighlightEventFlow
    {
        get => _highlightEventFlow;
        set
        {
            if (SetProperty(ref _highlightEventFlow, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HighlightAsyncFlow
    {
        get => _highlightAsyncFlow;
        set
        {
            if (SetProperty(ref _highlightAsyncFlow, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CancelAnalysisCommand.RaiseCanExecuteChanged();
                RefreshAnalysisCommand.RaiseCanExecuteChanged();
                ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasExportableResult
    {
        get => _hasExportableResult;
        private set
        {
            if (SetProperty(ref _hasExportableResult, value))
            {
                ExportJsonCommand.RaiseCanExecuteChanged();
                ExportViewModelJsonCommand.RaiseCanExecuteChanged();
                ExportDgmlCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSymbolCandidates
    {
        get => _hasSymbolCandidates;
        private set => SetProperty(ref _hasSymbolCandidates, value);
    }

    public bool HasGraphCanvas => GraphCanvasNodes.Count > 0;

    public bool HasSelectedNodeDetails => SelectedNode is not null;

    public bool HasSelectedNodeLocation => !string.IsNullOrWhiteSpace(SelectedNodeLocation);

    public bool HasSelectedNodeSignature => !string.IsNullOrWhiteSpace(SelectedNodeSignature);

    public bool HasSelectedNodeSummary => !string.IsNullOrWhiteSpace(SelectedNodeSummaryText);

    public bool HasSelectedNodeStats => SelectedNodeStats.Count > 0;

    public bool HasSelectedNodeImpactSummary => SelectedNodeImpactSummary.Count > 0;

    public bool HasSelectedNodeRiskSummary => SelectedNodeRiskSummary.Count > 0;

    public bool HasSelectedNodeHighlights => SelectedNodeHighlights.Count > 0;

    public double GraphCanvasWidth
    {
        get => _graphCanvasWidth;
        private set => SetProperty(ref _graphCanvasWidth, value);
    }

    public double GraphCanvasHeight
    {
        get => _graphCanvasHeight;
        private set => SetProperty(ref _graphCanvasHeight, value);
    }

    public double MiniMapWidth
    {
        get => _miniMapWidth;
        private set => SetProperty(ref _miniMapWidth, value);
    }

    public double MiniMapHeight
    {
        get => _miniMapHeight;
        private set => SetProperty(ref _miniMapHeight, value);
    }

    public double MiniMapViewportLeft
    {
        get => _miniMapViewportLeft;
        private set => SetProperty(ref _miniMapViewportLeft, value);
    }

    public double MiniMapViewportTop
    {
        get => _miniMapViewportTop;
        private set => SetProperty(ref _miniMapViewportTop, value);
    }

    public double MiniMapViewportWidth
    {
        get => _miniMapViewportWidth;
        private set => SetProperty(ref _miniMapViewportWidth, value);
    }

    public double MiniMapViewportHeight
    {
        get => _miniMapViewportHeight;
        private set => SetProperty(ref _miniMapViewportHeight, value);
    }

    public string SelectedNodeTitle
    {
        get => _selectedNodeTitle;
        private set => SetProperty(ref _selectedNodeTitle, value);
    }

    public string SelectedNodeSubtitle
    {
        get => _selectedNodeSubtitle;
        private set => SetProperty(ref _selectedNodeSubtitle, value);
    }

    public string SelectedNodeProject
    {
        get => _selectedNodeProject;
        private set => SetProperty(ref _selectedNodeProject, value);
    }

    public string SelectedNodeLocation
    {
        get => _selectedNodeLocation;
        private set
        {
            if (SetProperty(ref _selectedNodeLocation, value))
            {
                OnPropertyChanged(nameof(HasSelectedNodeLocation));
            }
        }
    }

    public string SelectedNodeSignature
    {
        get => _selectedNodeSignature;
        private set
        {
            if (SetProperty(ref _selectedNodeSignature, value))
            {
                OnPropertyChanged(nameof(HasSelectedNodeSignature));
            }
        }
    }

    public string SelectedNodeSummaryText
    {
        get => _selectedNodeSummaryText;
        private set
        {
            if (SetProperty(ref _selectedNodeSummaryText, value))
            {
                OnPropertyChanged(nameof(HasSelectedNodeSummary));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedEdgeKind
    {
        get => _selectedEdgeKind;
        set
        {
            if (SetProperty(ref _selectedEdgeKind, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedNodeKind
    {
        get => _selectedNodeKind;
        set
        {
            if (SetProperty(ref _selectedNodeKind, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedProjectName
    {
        get => _selectedProjectName;
        set
        {
            if (SetProperty(ref _selectedProjectName, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedMinimumConfidence
    {
        get => _selectedMinimumConfidence;
        set
        {
            if (SetProperty(ref _selectedMinimumConfidence, value))
            {
                ApplyFiltersIfReady();
                ClearFiltersCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GraphCanvasDisplayMode SelectedDisplayMode
    {
        get => _selectedDisplayMode;
        set
        {
            if (SetProperty(ref _selectedDisplayMode, value))
            {
                UpdateLegendItems();
                ApplyFiltersIfReady();
            }
        }
    }

    public int SelectedDepth
    {
        get => _selectedDepth;
        set => SetProperty(ref _selectedDepth, value);
    }

    public bool ExcludeTests
    {
        get => _excludeTests;
        set => SetProperty(ref _excludeTests, value);
    }

    public bool ExcludeGenerated
    {
        get => _excludeGenerated;
        set => SetProperty(ref _excludeGenerated, value);
    }

    public bool FollowCaret
    {
        get => _followCaret;
        set
        {
            if (SetProperty(ref _followCaret, value) && _followCaretHandler is not null)
            {
                _ = _followCaretHandler(value);
            }
        }
    }

    public UsageMapNodeItemViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                LoadDetails(value?.Details);
                UpdateSelectedNodeDetails();
                UpdateBreadcrumbItems();
                OnPropertyChanged(nameof(HasSelectedNodeDetails));
                OpenSelectedNodeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public UsageMapCanvasNodeItemViewModel? SelectedCanvasNode
    {
        get => _selectedCanvasNode;
        private set
        {
            if (ReferenceEquals(_selectedCanvasNode, value))
            {
                return;
            }

            if (_selectedCanvasNode is not null)
            {
                _selectedCanvasNode.IsSelected = false;
            }

            _selectedCanvasNode = value;
            if (_selectedCanvasNode is not null)
            {
                _selectedCanvasNode.IsSelected = true;
                LoadDetails(_selectedCanvasNode.Details);
            }
            else
            {
                LoadDetails(null);
            }

            UpdateSelectedNodeDetails();
            UpdateBreadcrumbItems();
            OnPropertyChanged();
        }
    }

    public UsageMapNodeItemViewModel? RootNode
    {
        get => _rootNode;
        private set
        {
            if (SetProperty(ref _rootNode, value))
            {
                if (value is not null)
                {
                    SelectedNode = value;
                }

                OpenSelectedNodeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public UsageMapRelationItemViewModel? SelectedRelation
    {
        get => _selectedRelation;
        set
        {
            if (SetProperty(ref _selectedRelation, value))
            {
                LoadDetails(value?.Details);
                OpenSelectedRelationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public UsageMapSymbolCandidateItemViewModel? SelectedSymbolCandidate
    {
        get => _selectedSymbolCandidate;
        set
        {
            if (SetProperty(ref _selectedSymbolCandidate, value))
            {
                AnalyzeSelectedCandidateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public UsageMapRootSearchResultItemViewModel? SelectedRootSearchResult
    {
        get => _selectedRootSearchResult;
        set
        {
            if (SetProperty(ref _selectedRootSearchResult, value))
            {
                ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void BeginAnalysis(VisualStudioSymbolContext context, string statusMessage, CancellationTokenSource cancellation)
    {
        var previewNode = CreatePreviewNode(context);
        var preserveExistingContent = string.Equals(RootNode?.Id, previewNode.Id, StringComparison.Ordinal);

        _analysisCancellation?.Dispose();
        _analysisCancellation = cancellation;
        Title = $"Usage Map: {context.DisplayName}";
        Summary = preserveExistingContent ? "Refreshing..." : "Analyzing...";
        StatusMessage = statusMessage;
        ErrorMessage = string.Empty;
        IsBusy = true;
        _loadedModel = null;
        _activeModel = null;
        _collapsedCanvasNodeIds.Clear();
        HasExportableResult = false;
        Replace(SymbolCandidates, Array.Empty<UsageMapSymbolCandidateItemViewModel>());
        HasSymbolCandidates = false;
        SelectedSymbolCandidate = null;
        Replace(Diagnostics, Array.Empty<UsageMapDiagnosticItemViewModel>());
        OnPropertyChanged(nameof(HasDiagnostics));

        Replace(Nodes, [previewNode]);
        RootNode = previewNode;

        if (preserveExistingContent)
        {
            return;
        }

        Replace(GraphCanvasNodes, [ToCanvasPreviewNode(previewNode)]);
        Replace(GraphCanvasEdges, Array.Empty<UsageMapCanvasEdgeItemViewModel>());
        GraphCanvasWidth = 960d;
        GraphCanvasHeight = 180d;
        SelectedCanvasNode = GraphCanvasNodes.FirstOrDefault();
        OnPropertyChanged(nameof(HasGraphCanvas));
        UpdateBreadcrumbItems();
        UpdateMiniMapNodes();

        Replace(IncomingRelations, Array.Empty<UsageMapRelationItemViewModel>());
        Replace(OutgoingRelations, Array.Empty<UsageMapRelationItemViewModel>());
        Replace(RelatedRelations, Array.Empty<UsageMapRelationItemViewModel>());
        Replace(Details, previewNode.Details);

        SelectedRelation = null;
        UpdateProjectNames(Array.Empty<string>());
        UpdateNamespaceNames(Array.Empty<string>());
        UpdateAccessibilities(Array.Empty<string>());
    }

    public void ReportStatus(string statusMessage)
    {
        StatusMessage = statusMessage;
    }

    public AnalyzeOptions CreateAnalyzeOptions()
    {
        return new AnalyzeOptions
        {
            Depth = SelectedDepth,
            SymbolIndex = SelectedSymbolCandidate?.Index,
            ExcludeGenerated = ExcludeGenerated,
            ExcludeTests = ExcludeTests,
            WorkspaceLoader = "msbuild",
        };
    }

    public void SetRefreshHandler(Func<AnalyzeOptions, Task> refreshHandler)
    {
        _refreshHandler = refreshHandler;
        RefreshAnalysisCommand.RaiseCanExecuteChanged();
    }

    public void SetRootSearchHandlers(
        Func<string, CancellationToken, Task<IReadOnlyList<UsageMapRootSearchResultItemViewModel>>> rootSearchHandler,
        Func<UsageMapRootSearchResultItemViewModel, Task> applyRootSearchResultHandler)
    {
        _rootSearchHandler = rootSearchHandler;
        _applyRootSearchResultHandler = applyRootSearchResultHandler;
        ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
        ClearRootSearchCommand.RaiseCanExecuteChanged();
    }

    public void SetFollowCaretHandler(Func<bool, Task> followCaretHandler)
    {
        _followCaretHandler = followCaretHandler;
    }

    public void SetRerootHandler(Func<UsageMapCanvasNodeItemViewModel, Task> rerootHandler)
    {
        _rerootHandler = rerootHandler;
    }

    public void SetExportHandler(Func<UsageMapExportFormat, Task> exportHandler)
    {
        _exportHandler = exportHandler;
        ExportJsonCommand.RaiseCanExecuteChanged();
        ExportViewModelJsonCommand.RaiseCanExecuteChanged();
        ExportDgmlCommand.RaiseCanExecuteChanged();
    }

    public void Load(PresentationUsageMapViewModel model, PresentationGraphCanvasViewModel canvasModel)
    {
        _analysisCancellation?.Dispose();
        _analysisCancellation = null;
        StatusMessage = "Analysis completed.";
        ErrorMessage = string.Empty;
        IsBusy = false;
        _collapsedCanvasNodeIds.Clear();
        _loadedModel = model;
        _loadedCanvasModel = canvasModel;
        HasExportableResult = true;
        Title = model.Title;
        UpdateProjectNames(model);
        LoadSymbolCandidates(model);
        LoadDiagnostics(model);
        UpdateLegendItems();
        ApplyFilters();
    }

    public void MarkCanceled()
    {
        _analysisCancellation?.Dispose();
        _analysisCancellation = null;
        IsBusy = false;
        Summary = "Analysis canceled.";
        StatusMessage = "Analysis canceled.";
        ErrorMessage = string.Empty;
        HasExportableResult = false;
        _collapsedCanvasNodeIds.Clear();
        _loadedCanvasModel = null;
        Replace(Diagnostics, Array.Empty<UsageMapDiagnosticItemViewModel>());
        Replace(GraphCanvasNodes, Array.Empty<UsageMapCanvasNodeItemViewModel>());
        Replace(GraphCanvasEdges, Array.Empty<UsageMapCanvasEdgeItemViewModel>());
        Replace(MiniMapNodes, Array.Empty<UsageMapMiniMapNodeItemViewModel>());
        SelectedCanvasNode = null;
        OnPropertyChanged(nameof(HasGraphCanvas));
        OnPropertyChanged(nameof(HasDiagnostics));
    }

    public void ShowError(string message)
    {
        _analysisCancellation?.Dispose();
        _analysisCancellation = null;
        IsBusy = false;
        Summary = "Analysis failed.";
        StatusMessage = "Analysis failed.";
        ErrorMessage = message;
        HasExportableResult = false;
        _collapsedCanvasNodeIds.Clear();
        _loadedCanvasModel = null;
        Replace(Diagnostics, Array.Empty<UsageMapDiagnosticItemViewModel>());
        Replace(GraphCanvasNodes, Array.Empty<UsageMapCanvasNodeItemViewModel>());
        Replace(GraphCanvasEdges, Array.Empty<UsageMapCanvasEdgeItemViewModel>());
        Replace(MiniMapNodes, Array.Empty<UsageMapMiniMapNodeItemViewModel>());
        SelectedCanvasNode = null;
        OnPropertyChanged(nameof(HasGraphCanvas));
        OnPropertyChanged(nameof(HasDiagnostics));
    }

    private bool CanOpenSelectedNode()
    {
        return SelectedNode is not null && !string.IsNullOrWhiteSpace(SelectedNode.FilePath);
    }

    private async void OpenSelectedNode()
    {
        if (SelectedNode is null)
        {
            return;
        }

        await _navigationService.NavigateAsync(SelectedNode.FilePath, SelectedNode.LineNumber);
    }

    private bool CanOpenSelectedRelation()
    {
        return SelectedRelation is not null && !string.IsNullOrWhiteSpace(SelectedRelation.FilePath);
    }

    private bool CanCancelAnalysis()
    {
        return IsBusy && _analysisCancellation is not null;
    }

    private bool CanRefreshAnalysis()
    {
        return !IsBusy && _refreshHandler is not null;
    }

    private bool CanAnalyzeSelectedCandidate()
    {
        return !IsBusy && _refreshHandler is not null && SelectedSymbolCandidate is not null;
    }

    private bool CanExport()
    {
        return !IsBusy && HasExportableResult && _exportHandler is not null;
    }

    private bool CanClearFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchText) ||
               !string.Equals(SelectedEdgeKind, AllFilterValue, StringComparison.Ordinal) ||
               !string.Equals(SelectedNodeKind, AllFilterValue, StringComparison.Ordinal) ||
               !string.Equals(SelectedProjectName, AllFilterValue, StringComparison.Ordinal) ||
               !string.Equals(SelectedNamespaceName, AllFilterValue, StringComparison.Ordinal) ||
               !string.Equals(SelectedAccessibility, AllFilterValue, StringComparison.Ordinal) ||
               ExcludeExternalSymbols ||
               ExcludeSystemSymbols ||
               ExcludePackageSymbols ||
               HighlightInterfaceFlow ||
               HighlightEventFlow ||
               HighlightAsyncFlow ||
               CanvasFocusMode ||
               SelectedCanvasNodeLimit != 40 ||
               !string.Equals(SelectedMinimumConfidence, AllFilterValue, StringComparison.Ordinal);
    }

    private bool CanApplySelectedRootSearchResult()
    {
        return !IsBusy && _applyRootSearchResultHandler is not null && SelectedRootSearchResult is not null;
    }

    private bool CanClearRootSearch()
    {
        return !string.IsNullOrWhiteSpace(RootSearchText) || RootSearchResults.Count > 0;
    }

    private async void RefreshAnalysis()
    {
        if (_refreshHandler is null)
        {
            return;
        }

        await _refreshHandler(CreateAnalyzeOptions());
    }

    private void CancelAnalysis()
    {
        _analysisCancellation?.Cancel();
        StatusMessage = "Canceling...";
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedEdgeKind = AllFilterValue;
        SelectedNodeKind = AllFilterValue;
        SelectedProjectName = AllFilterValue;
        SelectedNamespaceName = AllFilterValue;
        SelectedAccessibility = AllFilterValue;
        ExcludeExternalSymbols = false;
        ExcludeSystemSymbols = false;
        ExcludePackageSymbols = false;
        HighlightInterfaceFlow = false;
        HighlightEventFlow = false;
        HighlightAsyncFlow = false;
        CanvasFocusMode = false;
        SelectedCanvasNodeLimit = 40;
        SelectedMinimumConfidence = AllFilterValue;
        ApplyFiltersIfReady();
    }

    private async void ApplySelectedRootSearchResult()
    {
        if (_applyRootSearchResultHandler is null || SelectedRootSearchResult is null)
        {
            return;
        }

        await _applyRootSearchResultHandler(SelectedRootSearchResult);
    }

    private void ClearRootSearch()
    {
        _rootSearchCancellation?.Cancel();
        _rootSearchCancellation?.Dispose();
        _rootSearchCancellation = null;
        RootSearchText = string.Empty;
        RootSearchStatusMessage = "Type 2 or more characters to search for a new root.";
        Replace(RootSearchResults, Array.Empty<UsageMapRootSearchResultItemViewModel>());
        SelectedRootSearchResult = null;
        OnPropertyChanged(nameof(HasRootSearchResults));
        ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
        ClearRootSearchCommand.RaiseCanExecuteChanged();
    }

    public Task ApplySelectedRootSearchResultAsync()
    {
        if (_applyRootSearchResultHandler is null || SelectedRootSearchResult is null)
        {
            return Task.CompletedTask;
        }

        return _applyRootSearchResultHandler(SelectedRootSearchResult);
    }

    private void ScheduleRootSearch()
    {
        _rootSearchCancellation?.Cancel();
        _rootSearchCancellation?.Dispose();
        _rootSearchCancellation = null;

        var query = RootSearchText.Trim();
        if (_rootSearchHandler is null)
        {
            return;
        }

        if (query.Length < 2)
        {
            RootSearchStatusMessage = "Type 2 or more characters to search for a new root.";
            Replace(RootSearchResults, Array.Empty<UsageMapRootSearchResultItemViewModel>());
            SelectedRootSearchResult = null;
            OnPropertyChanged(nameof(HasRootSearchResults));
            ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _rootSearchCancellation = cancellation;
        _ = RunRootSearchAsync(query, cancellation);
    }

    private async Task RunRootSearchAsync(string query, CancellationTokenSource cancellation)
    {
        try
        {
            RootSearchStatusMessage = $"Searching '{query}'...";
            await Task.Delay(250, cancellation.Token);
            var results = await _rootSearchHandler!(query, cancellation.Token);
            if (cancellation.Token.IsCancellationRequested)
            {
                return;
            }

            Replace(RootSearchResults, results);
            SelectedRootSearchResult = RootSearchResults.FirstOrDefault();
            RootSearchStatusMessage = RootSearchResults.Count == 0
                ? $"No source symbols matched '{query}'."
                : $"{RootSearchResults.Count} root candidates for '{query}'.";
            OnPropertyChanged(nameof(HasRootSearchResults));
            ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
            ClearRootSearchCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Replace(RootSearchResults, Array.Empty<UsageMapRootSearchResultItemViewModel>());
            SelectedRootSearchResult = null;
            RootSearchStatusMessage = ex.Message;
            OnPropertyChanged(nameof(HasRootSearchResults));
            ApplySelectedRootSearchResultCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            if (ReferenceEquals(_rootSearchCancellation, cancellation))
            {
                _rootSearchCancellation.Dispose();
                _rootSearchCancellation = null;
            }
        }
    }

    private async void AnalyzeSelectedCandidate()
    {
        if (_refreshHandler is null || SelectedSymbolCandidate is null)
        {
            return;
        }

        await _refreshHandler(CreateAnalyzeOptions());
    }

    private async void ExportJson()
    {
        if (_exportHandler is null)
        {
            return;
        }

        await _exportHandler(UsageMapExportFormat.Json);
    }

    private async void ExportViewModelJson()
    {
        if (_exportHandler is null)
        {
            return;
        }

        await _exportHandler(UsageMapExportFormat.ViewModelJson);
    }

    private async void ExportDgml()
    {
        if (_exportHandler is null)
        {
            return;
        }

        await _exportHandler(UsageMapExportFormat.Dgml);
    }

    private async void OpenSelectedRelation()
    {
        if (SelectedRelation is null)
        {
            return;
        }

        await _navigationService.NavigateAsync(SelectedRelation.FilePath, SelectedRelation.LineNumber);
    }

    private void LoadDetails(IReadOnlyList<UsageMapDetailItemViewModel>? items)
    {
        Replace(Details, items ?? Array.Empty<UsageMapDetailItemViewModel>());
    }

    private void UpdateSelectedNodeDetails()
    {
        if (SelectedNode is null)
        {
            SelectedNodeTitle = "No node selected";
            SelectedNodeSubtitle = string.Empty;
            SelectedNodeProject = string.Empty;
            SelectedNodeLocation = string.Empty;
            SelectedNodeSignature = string.Empty;
            SelectedNodeSummaryText = string.Empty;
            Replace(SelectedNodeStats, Array.Empty<UsageMapStatItemViewModel>());
            Replace(SelectedNodeImpactSummary, Array.Empty<UsageMapStatItemViewModel>());
            Replace(SelectedNodeRiskSummary, Array.Empty<UsageMapStatItemViewModel>());
            Replace(SelectedNodeHighlights, Array.Empty<UsageMapDetailItemViewModel>());
            OnPropertyChanged(nameof(HasSelectedNodeStats));
            OnPropertyChanged(nameof(HasSelectedNodeImpactSummary));
            OnPropertyChanged(nameof(HasSelectedNodeRiskSummary));
            OnPropertyChanged(nameof(HasSelectedNodeHighlights));
            return;
        }

        var selectedNode = SelectedNode;
        var details = selectedNode.Details ?? Array.Empty<UsageMapDetailItemViewModel>();
        SelectedNodeTitle = selectedNode.DisplayName;
        SelectedNodeSubtitle = BuildSelectedNodeSubtitle(selectedNode, SelectedCanvasNode);
        SelectedNodeProject = string.IsNullOrWhiteSpace(selectedNode.ProjectName)
            ? FindDetailValue(details, "projectName")
            : selectedNode.ProjectName;
        SelectedNodeLocation = BuildSelectedNodeLocation(selectedNode, details);
        SelectedNodeSignature = BuildSelectedNodeSignature(selectedNode, details);
        SelectedNodeSummaryText = FindDetailValue(details, "summary");
        Replace(SelectedNodeStats, BuildSelectedNodeStats(selectedNode));
        Replace(SelectedNodeImpactSummary, BuildSelectedNodeImpactSummary(selectedNode));
        Replace(SelectedNodeRiskSummary, BuildSelectedNodeRiskSummary(selectedNode));
        Replace(SelectedNodeHighlights, BuildSelectedNodeHighlights(details));
        OnPropertyChanged(nameof(HasSelectedNodeStats));
        OnPropertyChanged(nameof(HasSelectedNodeImpactSummary));
        OnPropertyChanged(nameof(HasSelectedNodeRiskSummary));
        OnPropertyChanged(nameof(HasSelectedNodeHighlights));
    }

    private void UpdateBreadcrumbItems()
    {
        var items = new List<UsageMapBreadcrumbItemViewModel>
        {
            new() { Label = SelectedDisplayMode.ToString() },
        };

        var projectName = !string.IsNullOrWhiteSpace(RootNode?.ProjectName)
            ? RootNode.ProjectName
            : SelectedNodeProject;
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            items.Add(new UsageMapBreadcrumbItemViewModel { Label = projectName });
        }

        if (!string.IsNullOrWhiteSpace(RootNode?.DisplayName))
        {
            items.Add(new UsageMapBreadcrumbItemViewModel { Label = RootNode.DisplayName });
        }

        if (SelectedNode is not null &&
            !string.Equals(SelectedNode.Id, RootNode?.Id, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(SelectedNode.DisplayName))
        {
            items.Add(new UsageMapBreadcrumbItemViewModel { Label = SelectedNode.DisplayName });
        }

        Replace(BreadcrumbItems, items);
    }

    private void UpdateLegendItems()
    {
        var items = SelectedDisplayMode switch
        {
            GraphCanvasDisplayMode.CallMap =>
            [
                new UsageMapLegendItemViewModel { Swatch = "#FF3A7A52", Label = "Outbound", Description = "Direct and interface calls from the root." },
                new UsageMapLegendItemViewModel { Swatch = "#FF5A6D8A", Label = "Inbound", Description = "Callers and call owners that reach the root." },
                new UsageMapLegendItemViewModel { Swatch = "#FF2F855A", Label = "Method", Description = "Executable method nodes." },
            ],
            GraphCanvasDisplayMode.DependencyMap =>
            [
                new UsageMapLegendItemViewModel { Swatch = "#FF6A7A88", Label = "Reference", Description = "Type usage, creation, and DI dependency edges." },
                new UsageMapLegendItemViewModel { Swatch = "#FF2B6CB0", Label = "Class", Description = "Concrete implementation and dependency targets." },
                new UsageMapLegendItemViewModel { Swatch = "#FF8B8B8B", Label = "External", Description = "Metadata-only dependency nodes." },
            ],
            GraphCanvasDisplayMode.InheritanceMap =>
            [
                new UsageMapLegendItemViewModel { Swatch = "#FF8A5A2B", Label = "Implements", Description = "Interface implementation and override edges." },
                new UsageMapLegendItemViewModel { Swatch = "#FF6B46C1", Label = "Interface", Description = "Interface or base contract nodes." },
                new UsageMapLegendItemViewModel { Swatch = "#FF2B6CB0", Label = "Class", Description = "Derived or implementing classes." },
            ],
            GraphCanvasDisplayMode.EventFlow =>
            [
                new UsageMapLegendItemViewModel { Swatch = "#FFC05621", Label = "Event", Description = "Publisher, event, and handler nodes." },
                new UsageMapLegendItemViewModel { Swatch = "#FF5A6D8A", Label = "Subscription", Description = "Subscription and raise flow edges." },
                new UsageMapLegendItemViewModel { Swatch = "#FF9A9A9A", Label = "Estimated", Description = "Estimated event dispatch edges." },
            ],
            _ => Array.Empty<UsageMapLegendItemViewModel>(),
        };

        Replace(LegendItems, items);
    }

    private void UpdateMiniMapNodes()
    {
        if (GraphCanvasNodes.Count == 0)
        {
            Replace(MiniMapNodes, Array.Empty<UsageMapMiniMapNodeItemViewModel>());
            MiniMapWidth = 180d;
            MiniMapHeight = 120d;
            MiniMapViewportLeft = 0d;
            MiniMapViewportTop = 0d;
            MiniMapViewportWidth = 0d;
            MiniMapViewportHeight = 0d;
            return;
        }

        const double maxWidth = 180d;
        const double maxHeight = 120d;
        var scale = Math.Min(maxWidth / GraphCanvasWidth, maxHeight / GraphCanvasHeight);
        scale = !double.IsNaN(scale) && !double.IsInfinity(scale) && scale > 0d ? scale : 1d;

        MiniMapWidth = Math.Max(120d, GraphCanvasWidth * scale);
        MiniMapHeight = Math.Max(72d, GraphCanvasHeight * scale);

        Replace(MiniMapNodes, GraphCanvasNodes.Select(node => new UsageMapMiniMapNodeItemViewModel
        {
            Id = node.Id,
            Left = node.Left * scale,
            Top = node.Top * scale,
            Width = Math.Max(4d, node.Width * scale),
            Height = Math.Max(3d, node.Height * scale),
            Fill = node.BorderBrush,
            Opacity = node.IsSelected || node.IsRoot ? 0.95d : Math.Max(0.18d, node.Opacity),
        }));
    }

    public void UpdateMiniMapViewport(
        double viewportWidth,
        double viewportHeight,
        double horizontalOffset,
        double verticalOffset,
        double zoom)
    {
        if (GraphCanvasWidth <= 0d || GraphCanvasHeight <= 0d || MiniMapWidth <= 0d || MiniMapHeight <= 0d || zoom <= 0d)
        {
            MiniMapViewportLeft = 0d;
            MiniMapViewportTop = 0d;
            MiniMapViewportWidth = 0d;
            MiniMapViewportHeight = 0d;
            return;
        }

        var scaleX = MiniMapWidth / GraphCanvasWidth;
        var scaleY = MiniMapHeight / GraphCanvasHeight;
        MiniMapViewportLeft = horizontalOffset / zoom * scaleX;
        MiniMapViewportTop = verticalOffset / zoom * scaleY;
        MiniMapViewportWidth = Math.Min(MiniMapWidth, viewportWidth / zoom * scaleX);
        MiniMapViewportHeight = Math.Min(MiniMapHeight, viewportHeight / zoom * scaleY);
    }

    private static UsageMapNodeItemViewModel ToNodeItem(Contracts.Presentation.UsageMapNodeViewModel node)
    {
        return new UsageMapNodeItemViewModel
        {
            Id = node.Id,
            DisplayName = node.DisplayName,
            SymbolKey = node.SymbolKey,
            Kind = node.Kind,
            ProjectName = node.ProjectName,
            NamespaceName = node.NamespaceName,
            Accessibility = node.Accessibility,
            FilePath = node.FilePath,
            LineNumber = node.LineNumber,
            IsRoot = node.IsRoot,
            IsExternal = node.IsExternal,
            ExternalCategory = node.ExternalCategory,
            Details = node.Details.Select(ToDetailItem).ToArray(),
        };
    }

    private static UsageMapNodeItemViewModel CreatePreviewNode(VisualStudioSymbolContext context)
    {
        var details = new List<UsageMapDetailItemViewModel>
        {
            new() { Key = "symbolKey", Value = context.SymbolKey },
            new() { Key = "projectName", Value = context.ProjectName },
            new() { Key = "filePath", Value = context.FilePath },
            new() { Key = "nodeKind", Value = context.Kind.ToString() },
        };

        if (context.LineNumber is not null)
        {
            details.Add(new UsageMapDetailItemViewModel
            {
                Key = "lineNumber",
                Value = context.LineNumber.Value.ToString(),
            });
        }

        return new UsageMapNodeItemViewModel
        {
            Id = context.SymbolKey,
            DisplayName = context.DisplayName,
            SymbolKey = context.SymbolKey,
            Kind = context.Kind,
            ProjectName = context.ProjectName,
            NamespaceName = string.Empty,
            Accessibility = string.Empty,
            FilePath = context.FilePath,
            LineNumber = context.LineNumber,
            IsRoot = true,
            IsExternal = false,
            ExternalCategory = string.Empty,
            Details = details,
        };
    }

    private static UsageMapCanvasNodeItemViewModel ToCanvasPreviewNode(UsageMapNodeItemViewModel node)
    {
        return new UsageMapCanvasNodeItemViewModel
        {
            Id = node.Id,
            DisplayName = node.DisplayName,
            SymbolKey = node.SymbolKey,
            Kind = node.Kind,
            ProjectName = node.ProjectName,
            FilePath = node.FilePath,
            LineNumber = node.LineNumber,
            IsRoot = true,
            Left = 350d,
            Top = 40d,
            Width = 260d,
            Height = 84d,
            Fill = ResolveAccentBrush(node.Kind, false),
            BorderBrush = "#FF395B84",
            KindLabel = ResolveKindLabel(node.Kind),
            HasChildren = false,
            Details = node.Details,
        };
    }

    private static UsageMapRelationItemViewModel ToRelationItem(Contracts.Presentation.UsageMapRelationViewModel relation)
    {
        var filePath = relation.Details.FirstOrDefault(static item => item.Key == "filePath")?.Value ?? string.Empty;
        var lineNumberValue = relation.Details.FirstOrDefault(static item => item.Key == "lineNumber")?.Value;
        _ = int.TryParse(lineNumberValue, out var lineNumber);

        return new UsageMapRelationItemViewModel
        {
            EdgeId = relation.EdgeId,
            Direction = relation.Direction,
            SourceNodeId = relation.SourceNodeId,
            SourceDisplayName = relation.SourceDisplayName,
            SourceKind = relation.SourceKind,
            TargetNodeId = relation.TargetNodeId,
            TargetDisplayName = relation.TargetDisplayName,
            TargetKind = relation.TargetKind,
            ProjectName = relation.ProjectName,
            NamespaceName = relation.NamespaceName,
            Accessibility = relation.Accessibility,
            IsExternal = relation.IsExternal,
            ExternalCategory = relation.ExternalCategory,
            EdgeKind = relation.EdgeKind,
            Label = relation.Label,
            Confidence = relation.Confidence,
            FilePath = filePath,
            LineNumber = lineNumber == 0 ? null : lineNumber,
            Details = relation.Details.Select(ToDetailItem).ToArray(),
        };
    }

    private static UsageMapDetailItemViewModel ToDetailItem(UsageMapDetailItem item)
    {
        return new UsageMapDetailItemViewModel
        {
            Key = item.Key,
            Value = item.Value,
        };
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private void ApplyFiltersIfReady()
    {
        if (_loadedModel is null || IsBusy)
        {
            return;
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_loadedModel is null)
        {
            return;
        }

        var filteredModel = CreateModeFilteredModel(_viewModelFilter.Apply(_loadedModel, CreateFilterCriteria()));
        _activeModel = filteredModel;
        Replace(Nodes, filteredModel.Nodes.Select(ToNodeItem));
        Replace(IncomingRelations, filteredModel.IncomingRelations.Select(ToRelationItem));
        Replace(OutgoingRelations, filteredModel.OutgoingRelations.Select(ToRelationItem));
        Replace(RelatedRelations, filteredModel.RelatedRelations.Select(ToRelationItem));
        ApplyCanvasModel(filteredModel);
        ApplyHighlighting();

        RootNode = Nodes.FirstOrDefault(static node => node.IsRoot) ?? ToNodeItem(filteredModel.RootNode);
        SelectedRelation = null;
        SelectedNode = RootNode;
        Summary = BuildSummary(filteredModel, _loadedModel);
        StatusMessage = BuildFilterStatus(filteredModel, _loadedModel);
    }

    private UsageMapFilterCriteria CreateFilterCriteria()
    {
        return new UsageMapFilterCriteria
        {
            SearchText = SearchText.Trim(),
            EdgeKind = ParseEdgeKind(SelectedEdgeKind),
            NodeKind = ParseNodeKind(SelectedNodeKind),
            ProjectName = string.Equals(SelectedProjectName, AllFilterValue, StringComparison.Ordinal)
                ? string.Empty
                : SelectedProjectName,
            NamespaceName = string.Equals(SelectedNamespaceName, AllFilterValue, StringComparison.Ordinal)
                ? string.Empty
                : SelectedNamespaceName,
            Accessibility = string.Equals(SelectedAccessibility, AllFilterValue, StringComparison.Ordinal)
                ? string.Empty
                : SelectedAccessibility,
            ExcludeExternalSymbols = ExcludeExternalSymbols,
            ExcludeSystemSymbols = ExcludeSystemSymbols,
            ExcludePackageSymbols = ExcludePackageSymbols,
            MinimumConfidence = ParseMinimumConfidence(SelectedMinimumConfidence),
        };
    }

    private void ApplyHighlighting()
    {
        if (!HasHighlightFocus)
        {
            ResetHighlightState();
            return;
        }

        var relations = IncomingRelations
            .Concat(OutgoingRelations)
            .Concat(RelatedRelations)
            .ToArray();
        var highlightedRelationIds = relations
            .Where(ShouldHighlightRelation)
            .Select(static relation => relation.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        var highlightedNodeIds = relations
            .Where(relation => highlightedRelationIds.Contains(relation.EdgeId))
            .SelectMany(static relation => new[] { relation.SourceNodeId, relation.TargetNodeId })
            .ToHashSet(StringComparer.Ordinal);

        if (RootNode is not null)
        {
            highlightedNodeIds.Add(RootNode.Id);
        }

        foreach (var relation in relations)
        {
            relation.Opacity = highlightedRelationIds.Contains(relation.EdgeId) ? 1d : 0.28d;
        }

        foreach (var node in GraphCanvasNodes)
        {
            var highlighted = node.IsRoot ||
                highlightedNodeIds.Contains(node.Id) ||
                ShouldHighlightCanvasNode(node);
            node.Opacity = highlighted ? 1d : 0.24d;
        }

        foreach (var edge in GraphCanvasEdges)
        {
            edge.Opacity = highlightedRelationIds.Contains(edge.Id) ? 1d : 0.16d;
        }

        UpdateMiniMapNodes();
    }

    private void ResetHighlightState()
    {
        foreach (var relation in IncomingRelations.Concat(OutgoingRelations).Concat(RelatedRelations))
        {
            relation.Opacity = 1d;
        }

        foreach (var node in GraphCanvasNodes)
        {
            node.Opacity = 1d;
        }

        foreach (var edge in GraphCanvasEdges)
        {
            edge.Opacity = 1d;
        }

        UpdateMiniMapNodes();
    }

    private bool ShouldHighlightRelation(UsageMapRelationItemViewModel relation)
    {
        return (HighlightInterfaceFlow && IsInterfaceRelation(relation)) ||
               (HighlightEventFlow && IsEventRelation(relation)) ||
               (HighlightAsyncFlow && IsAsyncRelation(relation));
    }

    private bool ShouldHighlightCanvasNode(UsageMapCanvasNodeItemViewModel node)
    {
        return (HighlightInterfaceFlow && node.Kind == NodeKind.Interface) ||
               (HighlightEventFlow && (node.Kind == NodeKind.Event || node.Kind == NodeKind.AnonymousFunction)) ||
               (HighlightAsyncFlow && ContainsAsyncMarker(node.DisplayName));
    }

    private static bool IsInterfaceRelation(UsageMapRelationItemViewModel relation)
    {
        return relation.EdgeKind is EdgeKind.InterfaceDispatch or EdgeKind.Implements or EdgeKind.Overrides ||
               relation.SourceKind == NodeKind.Interface ||
               relation.TargetKind == NodeKind.Interface;
    }

    private static bool IsEventRelation(UsageMapRelationItemViewModel relation)
    {
        return IsEventEdgeKind(relation.EdgeKind) ||
               relation.SourceKind == NodeKind.Event ||
               relation.TargetKind == NodeKind.Event ||
               relation.SourceKind == NodeKind.AnonymousFunction ||
               relation.TargetKind == NodeKind.AnonymousFunction;
    }

    private static bool IsAsyncRelation(UsageMapRelationItemViewModel relation)
    {
        return ContainsAsyncMarker(relation.SourceDisplayName) ||
               ContainsAsyncMarker(relation.TargetDisplayName) ||
               relation.Details.Any(static detail => ContainsAsyncMarker(detail.Value));
    }

    private static bool ContainsAsyncMarker(string value)
    {
        return value.IndexOf("Async", StringComparison.Ordinal) >= 0 ||
               value.IndexOf("await", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool HasHighlightFocus => HighlightInterfaceFlow || HighlightEventFlow || HighlightAsyncFlow;

    private PresentationUsageMapViewModel CreateModeFilteredModel(PresentationUsageMapViewModel model)
    {
        var incoming = model.IncomingRelations.Where(ShouldIncludeRelationInDisplayMode).ToArray();
        var outgoing = model.OutgoingRelations.Where(ShouldIncludeRelationInDisplayMode).ToArray();
        var related = model.RelatedRelations.Where(ShouldIncludeRelationInDisplayMode).ToArray();

        var visibleEdgeIds = incoming
            .Concat(outgoing)
            .Concat(related)
            .Select(static relation => relation.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        var visibleNodeIds = incoming
            .SelectMany(static relation => new[] { relation.SourceNodeId, relation.TargetNodeId })
            .Concat(outgoing.SelectMany(static relation => new[] { relation.SourceNodeId, relation.TargetNodeId }))
            .Concat(related.SelectMany(static relation => new[] { relation.SourceNodeId, relation.TargetNodeId }))
            .Append(model.RootNode.Id)
            .ToHashSet(StringComparer.Ordinal);

        return new PresentationUsageMapViewModel
        {
            Title = model.Title,
            RootNode = model.RootNode,
            SymbolResolution = model.SymbolResolution,
            Summary = new UsageMapSummaryViewModel
            {
                NodeCount = visibleNodeIds.Count,
                EdgeCount = visibleEdgeIds.Count,
                IncomingCount = incoming.Length,
                OutgoingCount = outgoing.Length,
                RelatedCount = related.Length,
                DiagnosticCount = model.Diagnostics.Count,
            },
            Nodes = model.Nodes.Where(node => visibleNodeIds.Contains(node.Id)).ToArray(),
            Edges = model.Edges.Where(edge => visibleEdgeIds.Contains(edge.Id)).ToArray(),
            IncomingRelations = incoming,
            OutgoingRelations = outgoing,
            RelatedRelations = related,
            Diagnostics = model.Diagnostics,
        };
    }

    private bool ShouldIncludeRelationInDisplayMode(UsageMapRelationViewModel relation)
    {
        return SelectedDisplayMode switch
        {
            GraphCanvasDisplayMode.CallMap => relation.EdgeKind is EdgeKind.DirectCall or
                EdgeKind.InterfaceDispatch or
                EdgeKind.DiResolvedCall or
                EdgeKind.InjectedByDi or
                EdgeKind.InstantiatedBy or
                EdgeKind.UnknownDynamicDispatch,
            GraphCanvasDisplayMode.DependencyMap => relation.EdgeKind is EdgeKind.Reference or
                EdgeKind.InstantiatedBy or
                EdgeKind.DiResolvedCall or
                EdgeKind.InjectedByDi,
            GraphCanvasDisplayMode.InheritanceMap => relation.EdgeKind is EdgeKind.Implements or EdgeKind.Overrides,
            GraphCanvasDisplayMode.EventFlow => IsEventEdgeKind(relation.EdgeKind),
            _ => true,
        };
    }

    private static EdgeKind? ParseEdgeKind(string edgeKind)
    {
        if (string.Equals(edgeKind, AllFilterValue, StringComparison.Ordinal))
        {
            return null;
        }

        return Enum.TryParse<EdgeKind>(edgeKind, out var parsed) ? parsed : null;
    }

    private static NodeKind? ParseNodeKind(string nodeKind)
    {
        if (string.Equals(nodeKind, AllFilterValue, StringComparison.Ordinal))
        {
            return null;
        }

        return Enum.TryParse<NodeKind>(nodeKind, out var parsed) ? parsed : null;
    }

    private static double? ParseMinimumConfidence(string confidence)
    {
        if (string.Equals(confidence, AllFilterValue, StringComparison.Ordinal))
        {
            return null;
        }

        return double.TryParse(confidence, out var parsed) ? parsed : null;
    }

    private void UpdateProjectNames(PresentationUsageMapViewModel model)
    {
        var projects = model.IncomingRelations
            .Concat(model.OutgoingRelations)
            .Concat(model.RelatedRelations)
            .Select(static relation => relation.ProjectName)
            .Where(static projectName => !string.IsNullOrWhiteSpace(projectName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static projectName => projectName, StringComparer.OrdinalIgnoreCase);

        UpdateProjectNames(projects);

        var namespaces = model.Nodes
            .Select(static node => node.NamespaceName)
            .Concat(model.IncomingRelations.Select(static relation => relation.NamespaceName))
            .Concat(model.OutgoingRelations.Select(static relation => relation.NamespaceName))
            .Concat(model.RelatedRelations.Select(static relation => relation.NamespaceName))
            .Where(static namespaceName => !string.IsNullOrWhiteSpace(namespaceName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static namespaceName => namespaceName, StringComparer.OrdinalIgnoreCase);

        UpdateNamespaceNames(namespaces);

        var accessibilities = model.Nodes
            .Select(static node => node.Accessibility)
            .Concat(model.IncomingRelations.Select(static relation => relation.Accessibility))
            .Concat(model.OutgoingRelations.Select(static relation => relation.Accessibility))
            .Concat(model.RelatedRelations.Select(static relation => relation.Accessibility))
            .Where(static accessibility => !string.IsNullOrWhiteSpace(accessibility))
            .SelectMany(static accessibility => accessibility.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(static value => value.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static accessibility => accessibility, StringComparer.OrdinalIgnoreCase);

        UpdateAccessibilities(accessibilities);
    }

    private void UpdateProjectNames(IEnumerable<string> projectNames)
    {
        Replace(AvailableProjectNames, new[] { AllFilterValue }.Concat(projectNames));

        if (!AvailableProjectNames.Any(name => string.Equals(name, SelectedProjectName, StringComparison.Ordinal)))
        {
            SelectedProjectName = AllFilterValue;
        }
    }

    private void UpdateNamespaceNames(IEnumerable<string> namespaceNames)
    {
        Replace(AvailableNamespaceNames, new[] { AllFilterValue }.Concat(namespaceNames));

        if (!AvailableNamespaceNames.Any(name => string.Equals(name, SelectedNamespaceName, StringComparison.Ordinal)))
        {
            SelectedNamespaceName = AllFilterValue;
        }
    }

    private void UpdateAccessibilities(IEnumerable<string> accessibilities)
    {
        Replace(AvailableAccessibilities, new[] { AllFilterValue }.Concat(accessibilities));

        if (!AvailableAccessibilities.Any(name => string.Equals(name, SelectedAccessibility, StringComparison.Ordinal)))
        {
            SelectedAccessibility = AllFilterValue;
        }
    }

    private string BuildSummary(PresentationUsageMapViewModel filteredModel, PresentationUsageMapViewModel originalModel)
    {
        return
            $"Mode: {SelectedDisplayMode}  " +
            $"Nodes: {filteredModel.Summary.NodeCount}/{originalModel.Summary.NodeCount}  " +
            $"Edges: {filteredModel.Summary.EdgeCount}/{originalModel.Summary.EdgeCount}  " +
            $"Incoming: {filteredModel.Summary.IncomingCount}/{originalModel.Summary.IncomingCount}  " +
            $"Outgoing: {filteredModel.Summary.OutgoingCount}/{originalModel.Summary.OutgoingCount}  " +
            $"Related: {filteredModel.Summary.RelatedCount}/{originalModel.Summary.RelatedCount}";
    }

    private string BuildFilterStatus(PresentationUsageMapViewModel filteredModel, PresentationUsageMapViewModel originalModel)
    {
        if (HasSymbolCandidates)
        {
            return $"Analysis requires candidate selection. {SymbolCandidates.Count} candidates found.";
        }

        if (filteredModel.Summary.EdgeCount == originalModel.Summary.EdgeCount &&
            string.IsNullOrWhiteSpace(SearchText) &&
            string.Equals(SelectedEdgeKind, AllFilterValue, StringComparison.Ordinal) &&
            string.Equals(SelectedNodeKind, AllFilterValue, StringComparison.Ordinal) &&
            string.Equals(SelectedProjectName, AllFilterValue, StringComparison.Ordinal) &&
            string.Equals(SelectedNamespaceName, AllFilterValue, StringComparison.Ordinal) &&
            string.Equals(SelectedAccessibility, AllFilterValue, StringComparison.Ordinal) &&
            !ExcludeExternalSymbols &&
            !ExcludeSystemSymbols &&
            !ExcludePackageSymbols &&
            !HighlightInterfaceFlow &&
            !HighlightEventFlow &&
            !HighlightAsyncFlow &&
            string.Equals(SelectedMinimumConfidence, AllFilterValue, StringComparison.Ordinal) &&
            SelectedDisplayMode == GraphCanvasDisplayMode.CallMap)
        {
            return "Analysis completed.";
        }

        var densitySuffix = CanvasFocusMode
            ? $", canvas focus on and node limit {SelectedCanvasNodeLimit}"
            : SelectedCanvasNodeLimit != 40
                ? $", canvas node limit {SelectedCanvasNodeLimit}"
                : string.Empty;
        return $"Analysis completed. Mode={SelectedDisplayMode}, filtered to {filteredModel.Summary.EdgeCount} edges{densitySuffix}.";
    }

    private void LoadSymbolCandidates(PresentationUsageMapViewModel model)
    {
        Replace(SymbolCandidates, model.SymbolResolution.Candidates.Select(candidate => new UsageMapSymbolCandidateItemViewModel
        {
            Index = candidate.Index,
            DisplayName = candidate.DisplayName,
            ProjectName = candidate.ProjectName,
            MatchKind = candidate.MatchKind,
            FilePath = candidate.FilePath,
            LineNumber = candidate.LineNumber,
            Kind = candidate.Kind,
        }));

        HasSymbolCandidates = model.SymbolResolution.Status is SymbolResolutionStatus.Ambiguous or SymbolResolutionStatus.InvalidSelection;
        SelectedSymbolCandidate = SymbolCandidates.FirstOrDefault(candidate => candidate.Index == model.SymbolResolution.SelectedSymbolIndex)
            ?? SymbolCandidates.FirstOrDefault();
        AnalyzeSelectedCandidateCommand.RaiseCanExecuteChanged();
    }

    private void LoadDiagnostics(PresentationUsageMapViewModel model)
    {
        Replace(Diagnostics, model.Diagnostics.Select(diagnostic => new UsageMapDiagnosticItemViewModel
        {
            Code = diagnostic.Code,
            Message = diagnostic.Message,
            Confidence = diagnostic.Confidence,
        }));
        OnPropertyChanged(nameof(HasDiagnostics));
    }

    private void ApplyCanvasModel(PresentationUsageMapViewModel filteredModel)
    {
        if (_loadedCanvasModel is null)
        {
            Replace(GraphCanvasNodes, Array.Empty<UsageMapCanvasNodeItemViewModel>());
            Replace(GraphCanvasEdges, Array.Empty<UsageMapCanvasEdgeItemViewModel>());
            GraphCanvasWidth = 960d;
            GraphCanvasHeight = 180d;
            OnPropertyChanged(nameof(HasGraphCanvas));
            return;
        }

        var visibleNodeIds = filteredModel.Nodes
            .Select(static node => node.Id)
            .Append(filteredModel.RootNode.Id)
            .ToHashSet(StringComparer.Ordinal);

        var filteredCanvasNodes = _loadedCanvasModel.Nodes
            .Where(node => visibleNodeIds.Contains(node.Id))
            .ToArray();

        if (filteredCanvasNodes.Length == 0)
        {
            Replace(GraphCanvasNodes, Array.Empty<UsageMapCanvasNodeItemViewModel>());
            Replace(GraphCanvasEdges, Array.Empty<UsageMapCanvasEdgeItemViewModel>());
            GraphCanvasWidth = 960d;
            GraphCanvasHeight = 180d;
            OnPropertyChanged(nameof(HasGraphCanvas));
            return;
        }

        var minX = filteredCanvasNodes.Min(static node => node.X);
        var minY = filteredCanvasNodes.Min(static node => node.Y);
        var maxX = filteredCanvasNodes.Max(static node => node.X + node.Width);
        var maxY = filteredCanvasNodes.Max(static node => node.Y + node.Height);

        const double leftPadding = 80d;
        const double topPadding = 32d;

        var nodeItems = filteredCanvasNodes
            .Select(node => ToCanvasNodeItem(node, minX, minY, leftPadding, topPadding))
            .ToArray();
        var nodeMap = nodeItems.ToDictionary(static node => node.Id, StringComparer.Ordinal);

        var visibleEdgeIds = filteredModel.IncomingRelations
            .Concat(filteredModel.OutgoingRelations)
            .Concat(filteredModel.RelatedRelations)
            .Select(static relation => relation.EdgeId)
            .ToHashSet(StringComparer.Ordinal);

        var visibleEdges = _loadedCanvasModel.Edges
            .Where(edge =>
                nodeMap.ContainsKey(edge.SourceId) &&
                nodeMap.ContainsKey(edge.TargetId) &&
                (visibleEdgeIds.Contains(edge.Id) ||
                 (visibleNodeIds.Contains(edge.SourceId) && visibleNodeIds.Contains(edge.TargetId))))
            .ToArray();
        var edgeItems = BuildCanvasEdgeItems(visibleEdges, nodeMap);
        var childMap = BuildCanvasChildMap(nodeItems, edgeItems);
        foreach (var node in nodeItems)
        {
            node.HasChildren = childMap.TryGetValue(node.Id, out var children) && children.Count > 0;
            node.IsCollapsed = _collapsedCanvasNodeIds.Contains(node.Id);
        }

        var hiddenNodeIds = CollectHiddenCanvasNodeIds(nodeItems, childMap);
        var visibleNodeItems = nodeItems.Where(node => !hiddenNodeIds.Contains(node.Id)).ToArray();
        var visibleEdgeItems = edgeItems
            .Where(edge => !hiddenNodeIds.Contains(edge.SourceId) && !hiddenNodeIds.Contains(edge.TargetId))
            .ToArray();

        var densityControlledNodeIds = ApplyCanvasDensityControl(
            visibleNodeItems,
            visibleEdgeItems,
            RootNode?.Id,
            SelectedCanvasNode?.Id);
        visibleNodeItems = visibleNodeItems
            .Where(node => densityControlledNodeIds.Contains(node.Id))
            .ToArray();
        visibleEdgeItems = visibleEdgeItems
            .Where(edge => densityControlledNodeIds.Contains(edge.SourceId) && densityControlledNodeIds.Contains(edge.TargetId))
            .ToArray();

        Replace(GraphCanvasNodes, visibleNodeItems.OrderBy(static node => node.IsRoot ? 0 : 1).ThenBy(static node => node.Left));
        Replace(GraphCanvasEdges, visibleEdgeItems);
        GraphCanvasWidth = Math.Max(960d, (maxX - minX) + (leftPadding * 2d));
        GraphCanvasHeight = Math.Max(180d, (maxY - minY) + (topPadding * 2d));
        UpdateMiniMapNodes();
        var selectedCanvasNode = GraphCanvasNodes.FirstOrDefault(node => node.Id == SelectedCanvasNode?.Id)
            ?? GraphCanvasNodes.FirstOrDefault(node => node.Id == RootNode?.Id)
            ?? GraphCanvasNodes.FirstOrDefault(static node => node.IsRoot)
            ?? GraphCanvasNodes.FirstOrDefault();
        SelectedCanvasNode = selectedCanvasNode;
        OnPropertyChanged(nameof(HasGraphCanvas));
    }

    private HashSet<string> ApplyCanvasDensityControl(
        IReadOnlyList<UsageMapCanvasNodeItemViewModel> nodeItems,
        IReadOnlyList<UsageMapCanvasEdgeItemViewModel> edgeItems,
        string? rootNodeId,
        string? selectedNodeId)
    {
        var remaining = nodeItems.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (remaining.Count <= SelectedCanvasNodeLimit)
        {
            if (!CanvasFocusMode)
            {
                return remaining;
            }
        }

        var adjacency = BuildCanvasAdjacency(edgeItems);
        if (CanvasFocusMode)
        {
            var focusId = !string.IsNullOrWhiteSpace(selectedNodeId) && remaining.Contains(selectedNodeId)
                ? selectedNodeId
                : !string.IsNullOrWhiteSpace(rootNodeId) && remaining.Contains(rootNodeId)
                    ? rootNodeId
                    : nodeItems.FirstOrDefault(static node => node.IsRoot)?.Id;
            if (!string.IsNullOrWhiteSpace(focusId))
            {
                remaining = CollectFocusNodeIds(focusId, adjacency, edgeItems);
            }
        }

        if (remaining.Count <= SelectedCanvasNodeLimit)
        {
            return remaining;
        }

        var candidateNodes = nodeItems
            .Where(node => remaining.Contains(node.Id))
            .ToArray();
        var degreeMap = BuildCanvasDegreeMap(edgeItems);
        var directRootNeighbors = BuildRootNeighborIds(rootNodeId, edgeItems);
        var prioritizedIds = candidateNodes
            .OrderByDescending(node => ComputeCanvasPriority(node, selectedNodeId, directRootNeighbors, degreeMap))
            .ThenBy(node => Math.Abs(node.Left))
            .ThenBy(node => Math.Abs(node.Top))
            .Take(SelectedCanvasNodeLimit)
            .Select(static node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(rootNodeId))
        {
            prioritizedIds.Add(rootNodeId);
        }

        return prioritizedIds;
    }

    private static Dictionary<string, HashSet<string>> BuildCanvasAdjacency(IReadOnlyList<UsageMapCanvasEdgeItemViewModel> edgeItems)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var edge in edgeItems)
        {
            AddNeighbor(adjacency, edge.SourceId, edge.TargetId);
            AddNeighbor(adjacency, edge.TargetId, edge.SourceId);
        }

        return adjacency;
    }

    private static Dictionary<string, int> BuildCanvasDegreeMap(IReadOnlyList<UsageMapCanvasEdgeItemViewModel> edgeItems)
    {
        var degreeMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var edge in edgeItems)
        {
            degreeMap[edge.SourceId] = degreeMap.TryGetValue(edge.SourceId, out var sourceCount) ? sourceCount + 1 : 1;
            degreeMap[edge.TargetId] = degreeMap.TryGetValue(edge.TargetId, out var targetCount) ? targetCount + 1 : 1;
        }

        return degreeMap;
    }

    private static HashSet<string> BuildRootNeighborIds(string? rootNodeId, IReadOnlyList<UsageMapCanvasEdgeItemViewModel> edgeItems)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(rootNodeId))
        {
            return ids;
        }

        foreach (var edge in edgeItems)
        {
            if (string.Equals(edge.SourceId, rootNodeId, StringComparison.Ordinal))
            {
                ids.Add(edge.TargetId);
            }
            else if (string.Equals(edge.TargetId, rootNodeId, StringComparison.Ordinal))
            {
                ids.Add(edge.SourceId);
            }
        }

        return ids;
    }

    private static HashSet<string> CollectFocusNodeIds(
        string focusId,
        IReadOnlyDictionary<string, HashSet<string>> adjacency,
        IReadOnlyList<UsageMapCanvasEdgeItemViewModel> edgeItems)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { focusId };
        if (adjacency.TryGetValue(focusId, out var firstHop))
        {
            foreach (var neighbor in firstHop)
            {
                ids.Add(neighbor);
            }
        }

        foreach (var edge in edgeItems)
        {
            if (ids.Contains(edge.SourceId) || ids.Contains(edge.TargetId))
            {
                ids.Add(edge.SourceId);
                ids.Add(edge.TargetId);
            }
        }

        return ids;
    }

    private static double ComputeCanvasPriority(
        UsageMapCanvasNodeItemViewModel node,
        string? selectedNodeId,
        ISet<string> directRootNeighbors,
        IReadOnlyDictionary<string, int> degreeMap)
    {
        var score = 0d;
        if (node.IsRoot)
        {
            score += 1000d;
        }

        if (!string.IsNullOrWhiteSpace(selectedNodeId) && string.Equals(node.Id, selectedNodeId, StringComparison.Ordinal))
        {
            score += 500d;
        }

        if (directRootNeighbors.Contains(node.Id))
        {
            score += 220d;
        }

        score += node.Lane switch
        {
            CanvasNodeLane.Center => 120d,
            CanvasNodeLane.Inbound or CanvasNodeLane.Outbound => 80d,
            _ => 20d,
        };
        score -= Math.Min(60d, Math.Abs(node.Left) / 10d);
        score -= Math.Min(60d, Math.Abs(node.Top) / 18d);
        score += degreeMap.TryGetValue(node.Id, out var degree) ? degree * 8d : 0d;
        score += node.IsExternal ? -35d : 10d;
        score += node.Kind switch
        {
            NodeKind.Method => 16d,
            NodeKind.Interface => 12d,
            NodeKind.Class => 10d,
            NodeKind.Event => 8d,
            _ => 0d,
        };

        return score;
    }

    private static void AddNeighbor(
        IDictionary<string, HashSet<string>> adjacency,
        string sourceId,
        string targetId)
    {
        if (!adjacency.TryGetValue(sourceId, out var neighbors))
        {
            neighbors = new HashSet<string>(StringComparer.Ordinal);
            adjacency[sourceId] = neighbors;
        }

        neighbors.Add(targetId);
    }

    private static UsageMapCanvasNodeItemViewModel ToCanvasNodeItem(
        CanvasNodeViewModel node,
        double minX,
        double minY,
        double leftPadding,
        double topPadding)
    {
        return new UsageMapCanvasNodeItemViewModel
        {
            Id = node.Id,
            DisplayName = node.DisplayName,
            SymbolKey = node.SymbolKey,
            Kind = node.Kind,
            ProjectName = node.ProjectName,
            FilePath = node.FilePath,
            LineNumber = node.LineNumber,
            IsRoot = node.IsRoot,
            IsExternal = node.IsExternal,
            ExternalCategory = node.ExternalCategory,
            Lane = node.Lane,
            Left = node.X - minX + leftPadding,
            Top = node.Y - minY + topPadding,
            Width = node.Width,
            Height = node.Height,
            Fill = ResolveAccentBrush(node.Kind, node.IsExternal),
            BorderBrush = ResolveBorderBrush(node.Kind, node.IsRoot, node.IsExternal),
            KindLabel = ResolveKindLabel(node.Kind),
            HasChildren = false,
            Details = node.Details.Select(ToDetailItem).ToArray(),
        };
    }

    private static UsageMapCanvasEdgeItemViewModel[] BuildCanvasEdgeItems(
        IReadOnlyList<CanvasEdgeViewModel> edges,
        IReadOnlyDictionary<string, UsageMapCanvasNodeItemViewModel> nodeMap)
    {
        var edgeContexts = edges
            .Select(edge => CreateEdgeRouteContext(edge, nodeMap[edge.SourceId], nodeMap[edge.TargetId]))
            .ToArray();

        var startOffsets = BuildAnchorOffsets(
            edgeContexts,
            static context => context.Source.Id,
            static context => context.SourceSide,
            static context => context.EndY);
        var endOffsets = BuildAnchorOffsets(
            edgeContexts,
            static context => context.Target.Id,
            static context => context.TargetSide,
            static context => context.StartY);
        var bundleOffsets = BuildBundleOffsets(edgeContexts);

        return edgeContexts
            .Select(context => ToCanvasEdgeItem(
                context,
                startOffsets[(context.Edge.Id, context.Source.Id, context.SourceSide)],
                endOffsets[(context.Edge.Id, context.Target.Id, context.TargetSide)],
                bundleOffsets[context.Edge.Id]))
            .ToArray();
    }

    private static EdgeRouteContext CreateEdgeRouteContext(
        CanvasEdgeViewModel edge,
        UsageMapCanvasNodeItemViewModel source,
        UsageMapCanvasNodeItemViewModel target)
    {
        var flowsLeftToRight = source.Left <= target.Left;
        var sourceSide = flowsLeftToRight ? AnchorSide.Right : AnchorSide.Left;
        var targetSide = flowsLeftToRight ? AnchorSide.Left : AnchorSide.Right;

        return new EdgeRouteContext(
            edge,
            source,
            target,
            sourceSide,
            targetSide,
            GetAnchorX(source, sourceSide),
            GetAnchorY(source),
            GetAnchorX(target, targetSide),
            GetAnchorY(target));
    }

    private static Dictionary<(string EdgeId, string NodeId, AnchorSide Side), double> BuildAnchorOffsets(
        IReadOnlyList<EdgeRouteContext> contexts,
        Func<EdgeRouteContext, string> nodeSelector,
        Func<EdgeRouteContext, AnchorSide> sideSelector,
        Func<EdgeRouteContext, double> orderSelector)
    {
        var result = new Dictionary<(string EdgeId, string NodeId, AnchorSide Side), double>();
        foreach (var group in contexts
                     .GroupBy(context => (NodeId: nodeSelector(context), Side: sideSelector(context)))
                     .OrderBy(static group => group.Key.NodeId, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(orderSelector).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                result[(ordered[index].Edge.Id, group.Key.NodeId, group.Key.Side)] =
                    ComputeCenteredOffset(index, ordered.Length, 12d);
            }
        }

        return result;
    }

    private static Dictionary<string, double> BuildBundleOffsets(IReadOnlyList<EdgeRouteContext> contexts)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var group in contexts
                     .GroupBy(CreateBundleKey)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var ordered = group
                .OrderBy(static context => Math.Min(context.StartY, context.EndY))
                .ThenBy(static context => Math.Max(context.StartY, context.EndY))
                .ToArray();

            for (var index = 0; index < ordered.Length; index++)
            {
                result[ordered[index].Edge.Id] = ComputeCenteredOffset(index, ordered.Length, 14d);
            }
        }

        return result;
    }

    private static string CreateBundleKey(EdgeRouteContext context)
    {
        return context.Edge.Lane switch
        {
            CanvasNodeLane.Inbound => $"in:{context.Target.Id}",
            CanvasNodeLane.Outbound => $"out:{context.Source.Id}",
            _ => $"rel:{Math.Min(context.Source.Left, context.Target.Left).ToString("F0", CultureInfo.InvariantCulture)}:{Math.Max(context.Source.Left, context.Target.Left).ToString("F0", CultureInfo.InvariantCulture)}",
        };
    }

    private static UsageMapCanvasEdgeItemViewModel ToCanvasEdgeItem(
        EdgeRouteContext context,
        double startOffset,
        double endOffset,
        double bundleOffset)
    {
        var startX = context.StartX;
        var startY = context.StartY + startOffset;
        var endX = context.EndX;
        var endY = context.EndY + endOffset;

        var pathData = context.Edge.Lane switch
        {
            CanvasNodeLane.Inbound => BuildHorizontalBundlePath(startX, startY, endX, endY, endX - 72d + bundleOffset),
            CanvasNodeLane.Outbound => BuildHorizontalBundlePath(startX, startY, endX, endY, startX + 72d + bundleOffset),
            _ => BuildRelatedBundlePath(startX, startY, endX, endY, Math.Max(startY, endY) + 42d + bundleOffset),
        };

        return new UsageMapCanvasEdgeItemViewModel
        {
            Id = context.Edge.Id,
            SourceId = context.Edge.SourceId,
            TargetId = context.Edge.TargetId,
            Kind = context.Edge.Kind,
            Lane = context.Edge.Lane,
            Stroke = ResolveEdgeStroke(context.Edge.Style, context.Edge.Lane),
            StrokeThickness = context.Edge.Style == CanvasEdgeStyle.Bold ? 2.5d : 1.5d,
            PathData = pathData,
        };
    }

    private static string BuildHorizontalBundlePath(
        double startX,
        double startY,
        double endX,
        double endY,
        double bundleX)
    {
        return FormattableString.Invariant(
            $"M {startX:F1},{startY:F1} C {bundleX:F1},{startY:F1} {bundleX:F1},{endY:F1} {endX:F1},{endY:F1}");
    }

    private static string BuildRelatedBundlePath(
        double startX,
        double startY,
        double endX,
        double endY,
        double bundleY)
    {
        return FormattableString.Invariant(
            $"M {startX:F1},{startY:F1} C {startX:F1},{bundleY:F1} {endX:F1},{bundleY:F1} {endX:F1},{endY:F1}");
    }

    private static double GetAnchorX(UsageMapCanvasNodeItemViewModel node, AnchorSide side)
    {
        return side == AnchorSide.Left ? node.Left : node.Left + node.Width;
    }

    private static double GetAnchorY(UsageMapCanvasNodeItemViewModel node)
    {
        return node.Top + (node.Height / 2d);
    }

    private static double ComputeCenteredOffset(int index, int count, double spacing)
    {
        return (index - ((count - 1) / 2d)) * spacing;
    }

    private static string ResolveAccentBrush(NodeKind kind, bool isExternal)
    {
        if (isExternal)
        {
            return "#FFE7E7E7";
        }

        return kind switch
        {
            NodeKind.Class => "#FFDDEBFF",
            NodeKind.Interface => "#FFE9DFFF",
            NodeKind.Method => "#FFDCF6E4",
            NodeKind.Property => "#FFF7F0D8",
            NodeKind.Event => "#FFFBE2D6",
            _ => "#FFF3F4F6",
        };
    }

    private static string ResolveBorderBrush(NodeKind kind, bool isRoot, bool isExternal)
    {
        if (isExternal)
        {
            return "#FF8B8B8B";
        }

        var stroke = kind switch
        {
            NodeKind.Class => "#FF2B6CB0",
            NodeKind.Interface => "#FF6B46C1",
            NodeKind.Method => "#FF2F855A",
            NodeKind.Property => "#FFB7791F",
            NodeKind.Event => "#FFC05621",
            _ => "#FF718096",
        };

        return stroke;
    }

    private static string ResolveEdgeStroke(CanvasEdgeStyle style, CanvasNodeLane lane)
    {
        if (lane == CanvasNodeLane.Inbound)
        {
            return "#FF5A6D8A";
        }

        if (lane == CanvasNodeLane.Outbound)
        {
            return "#FF3A7A52";
        }

        return style switch
        {
            CanvasEdgeStyle.Bold => "#FF8A5A2B",
            CanvasEdgeStyle.Dashed => "#FF7A7A7A",
            CanvasEdgeStyle.Dotted => "#FF9A9A9A",
            _ => "#FF6A7A88",
        };
    }

    private static string ResolveKindLabel(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Class => "C",
            NodeKind.Interface => "I",
            NodeKind.Method => "M",
            NodeKind.Property => "P",
            NodeKind.Event => "E",
            _ => "?",
        };
    }

    private enum AnchorSide
    {
        Left = 0,
        Right,
    }

    public void SelectCanvasNode(UsageMapCanvasNodeItemViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        SelectedCanvasNode = node;
        if (RootNode is not null && string.Equals(RootNode.Id, node.Id, StringComparison.Ordinal))
        {
            SelectedNode = RootNode;
            return;
        }

        SelectedNode = new UsageMapNodeItemViewModel
        {
            Id = node.Id,
            DisplayName = node.DisplayName,
            SymbolKey = node.SymbolKey,
            Kind = node.Kind,
            ProjectName = node.ProjectName,
            NamespaceName = FindDetailValue(node.Details, "namespaceName"),
            Accessibility = FindDetailValue(node.Details, "accessibility"),
            FilePath = node.FilePath,
            LineNumber = node.LineNumber,
            IsRoot = node.IsRoot,
            IsExternal = node.IsExternal,
            ExternalCategory = node.ExternalCategory,
            Details = node.Details,
        };
    }

    public async Task OpenCanvasNodeAsync(UsageMapCanvasNodeItemViewModel? node)
    {
        if (node is null || string.IsNullOrWhiteSpace(node.FilePath))
        {
            return;
        }

        SelectCanvasNode(node);
        await _navigationService.NavigateAsync(node.FilePath, node.LineNumber);
    }

    public async Task RerootCanvasNodeAsync(UsageMapCanvasNodeItemViewModel? node)
    {
        if (node is null || _rerootHandler is null || string.IsNullOrWhiteSpace(node.SymbolKey))
        {
            return;
        }

        SelectCanvasNode(node);
        await _rerootHandler(node);
    }

    public void ToggleCanvasNodeCollapse(UsageMapCanvasNodeItemViewModel? node)
    {
        if (node is null || !node.HasChildren)
        {
            return;
        }

        if (_collapsedCanvasNodeIds.Contains(node.Id))
        {
            _collapsedCanvasNodeIds.Remove(node.Id);
        }
        else
        {
            _collapsedCanvasNodeIds.Add(node.Id);
        }

        ApplyFiltersIfReady();
    }

    private string BuildSelectedNodeSubtitle(
        UsageMapNodeItemViewModel node,
        UsageMapCanvasNodeItemViewModel? canvasNode)
    {
        var segments = new List<string> { node.Kind.ToString() };

        if (node.IsRoot)
        {
            segments.Add("Root");
        }

        if (canvasNode is not null && string.Equals(canvasNode.Id, node.Id, StringComparison.Ordinal))
        {
            if (canvasNode.IsExternal)
            {
                segments.Add("External");
            }

            segments.Add(canvasNode.Lane.ToString());
        }

        return string.Join("  •  ", segments.Distinct(StringComparer.Ordinal));
    }

    private static string BuildSelectedNodeLocation(
        UsageMapNodeItemViewModel node,
        IReadOnlyList<UsageMapDetailItemViewModel> details)
    {
        var filePath = string.IsNullOrWhiteSpace(node.FilePath)
            ? FindDetailValue(details, "filePath")
            : node.FilePath;
        var lineValue = node.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? FindDetailValue(details, "lineNumber");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(lineValue) ? filePath : $"{filePath}:{lineValue}";
    }

    private static string BuildSelectedNodeSignature(
        UsageMapNodeItemViewModel node,
        IReadOnlyList<UsageMapDetailItemViewModel> details)
    {
        var explicitSignature = FindDetailValue(details, "signature");
        if (!string.IsNullOrWhiteSpace(explicitSignature))
        {
            return explicitSignature;
        }

        var containingType = FindDetailValue(details, "containingTypeName");
        if (!string.IsNullOrWhiteSpace(containingType) &&
            (node.Kind == NodeKind.Method || node.Kind == NodeKind.Property || node.Kind == NodeKind.Event))
        {
            return $"{containingType}.{node.DisplayName}";
        }

        return node.DisplayName;
    }

    private IEnumerable<UsageMapStatItemViewModel> BuildSelectedNodeStats(UsageMapNodeItemViewModel node)
    {
        var edges = _activeModel?.Edges ?? Array.Empty<UsageMapEdgeViewModel>();
        var incomingCount = edges.Count(edge => string.Equals(edge.TargetId, node.Id, StringComparison.Ordinal));
        var outgoingCount = edges.Count(edge => string.Equals(edge.SourceId, node.Id, StringComparison.Ordinal));
        var relatedCount = edges.Count(edge =>
            edge.Lane == CanvasNodeLane.Related &&
            (string.Equals(edge.SourceId, node.Id, StringComparison.Ordinal) ||
             string.Equals(edge.TargetId, node.Id, StringComparison.Ordinal)));
        var implementationCount = edges.Count(edge =>
            (edge.Kind == EdgeKind.Implements || edge.Kind == EdgeKind.Overrides) &&
            (string.Equals(edge.SourceId, node.Id, StringComparison.Ordinal) ||
             string.Equals(edge.TargetId, node.Id, StringComparison.Ordinal)));
        var eventCount = edges.Count(edge =>
            IsEventEdgeKind(edge.Kind) &&
            (string.Equals(edge.SourceId, node.Id, StringComparison.Ordinal) ||
             string.Equals(edge.TargetId, node.Id, StringComparison.Ordinal)));

        yield return new UsageMapStatItemViewModel { Label = "Kind", Value = node.Kind.ToString() };
        if (!string.IsNullOrWhiteSpace(node.Accessibility))
        {
            yield return new UsageMapStatItemViewModel { Label = "Access", Value = node.Accessibility };
        }
        yield return new UsageMapStatItemViewModel { Label = "Incoming", Value = incomingCount.ToString(CultureInfo.InvariantCulture) };
        yield return new UsageMapStatItemViewModel { Label = "Outgoing", Value = outgoingCount.ToString(CultureInfo.InvariantCulture) };

        if (relatedCount > 0)
        {
            yield return new UsageMapStatItemViewModel { Label = "Related", Value = relatedCount.ToString(CultureInfo.InvariantCulture) };
        }

        if (implementationCount > 0)
        {
            yield return new UsageMapStatItemViewModel { Label = "Implement", Value = implementationCount.ToString(CultureInfo.InvariantCulture) };
        }

        if (eventCount > 0)
        {
            yield return new UsageMapStatItemViewModel { Label = "Events", Value = eventCount.ToString(CultureInfo.InvariantCulture) };
        }

        var complexity = FindDetailValue(node.Details, "complexity");
        if (!string.IsNullOrWhiteSpace(complexity))
        {
            yield return new UsageMapStatItemViewModel { Label = "Complexity", Value = complexity };
        }

        if (SelectedCanvasNode is not null &&
            string.Equals(SelectedCanvasNode.Id, node.Id, StringComparison.Ordinal) &&
            SelectedCanvasNode.IsExternal)
        {
            var category = string.IsNullOrWhiteSpace(SelectedCanvasNode.ExternalCategory)
                ? "External"
                : SelectedCanvasNode.ExternalCategory;
            yield return new UsageMapStatItemViewModel { Label = "Origin", Value = category };
        }
        else if (node.IsExternal)
        {
            var category = string.IsNullOrWhiteSpace(node.ExternalCategory)
                ? "External"
                : node.ExternalCategory;
            yield return new UsageMapStatItemViewModel { Label = "Origin", Value = category };
        }
    }

    private IEnumerable<UsageMapStatItemViewModel> BuildSelectedNodeImpactSummary(UsageMapNodeItemViewModel node)
    {
        if (_activeModel is null)
        {
            yield break;
        }

        var impact = _nodeAssessmentBuilder.Build(_activeModel, node.Id).Impact;

        yield return new UsageMapStatItemViewModel
        {
            Label = "Referencing Projects",
            Value = impact.ReferencingProjectCount.ToString(CultureInfo.InvariantCulture),
        };
        yield return new UsageMapStatItemViewModel
        {
            Label = "Implementations",
            Value = impact.ImplementationCount.ToString(CultureInfo.InvariantCulture),
        };
        yield return new UsageMapStatItemViewModel
        {
            Label = "Overrides",
            Value = impact.OverrideCount.ToString(CultureInfo.InvariantCulture),
        };
        yield return new UsageMapStatItemViewModel
        {
            Label = "Test References",
            Value = impact.HasTestReference ? "Yes" : "No",
        };
    }

    private IEnumerable<UsageMapStatItemViewModel> BuildSelectedNodeRiskSummary(UsageMapNodeItemViewModel node)
    {
        if (_activeModel is null)
        {
            yield break;
        }

        var risk = _nodeAssessmentBuilder.Build(_activeModel, node.Id).Risk;
        var driverText = risk.Drivers.Count == 0 ? "local change" : string.Join(", ", risk.Drivers);

        yield return new UsageMapStatItemViewModel
        {
            Label = "Risk Score",
            Value = risk.RiskScore.ToString(CultureInfo.InvariantCulture),
        };
        yield return new UsageMapStatItemViewModel
        {
            Label = "Risk Level",
            Value = risk.RiskLevel,
        };
        yield return new UsageMapStatItemViewModel
        {
            Label = "Public API",
            Value = risk.IsPublicApi ? "Yes" : "No",
        };
        yield return new UsageMapStatItemViewModel
        {
            Label = "Drivers",
            Value = driverText,
        };
    }

    private static IEnumerable<UsageMapDetailItemViewModel> BuildSelectedNodeHighlights(
        IReadOnlyList<UsageMapDetailItemViewModel> details)
    {
        string[] hiddenKeys =
        [
            "symbolKey",
            "projectName",
            "filePath",
            "lineNumber",
            "nodeKind",
            "signature",
            "summary",
        ];

        string[] preferredOrder =
        [
            "containingTypeName",
            "publisherTypeName",
            "eventName",
            "handlerName",
            "handlerKind",
            "targetSymbol",
            "referenceText",
            "syntaxKind",
            "symbolOrigin",
            "normalizationStrategy",
            "assemblyIdentity",
            "limitation",
            "isOverride",
            "isUnsubscribed",
            "confidence",
        ];

        return details
            .Where(item => !hiddenKeys.Any(key => string.Equals(key, item.Key, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item =>
            {
                var index = Array.FindIndex(
                    preferredOrder,
                    key => string.Equals(key, item.Key, StringComparison.OrdinalIgnoreCase));
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(item => new UsageMapDetailItemViewModel
            {
                Key = FormatDetailLabel(item.Key),
                Value = item.Value,
            });
    }

    private static bool IsEventEdgeKind(EdgeKind edgeKind)
    {
        return edgeKind is EdgeKind.ContainsSubscription or
            EdgeKind.EventSubscription or
            EdgeKind.EventUnsubscription or
            EdgeKind.EventHandlerTarget or
            EdgeKind.EventRaise or
            EdgeKind.EventDispatchEstimated;
    }

    private static string FindDetailValue(IReadOnlyList<UsageMapDetailItemViewModel> details, string key)
    {
        return details.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))?.Value
            ?? string.Empty;
    }

    private static string FormatDetailLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var builder = new List<char>(key.Length + 8);
        for (var index = 0; index < key.Length; index++)
        {
            var current = key[index];
            if (index > 0 && char.IsUpper(current) && !char.IsWhiteSpace(builder[builder.Count - 1]))
            {
                builder.Add(' ');
            }

            builder.Add(index == 0 ? char.ToUpperInvariant(current) : current);
        }

        return new string(builder.ToArray());
    }

    private static Dictionary<string, HashSet<string>> BuildCanvasChildMap(
        IReadOnlyList<UsageMapCanvasNodeItemViewModel> nodeItems,
        IReadOnlyList<UsageMapCanvasEdgeItemViewModel> edgeItems)
    {
        var nodeMap = nodeItems.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var childMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var edge in edgeItems)
        {
            if (!nodeMap.TryGetValue(edge.SourceId, out var source) || !nodeMap.TryGetValue(edge.TargetId, out var target))
            {
                continue;
            }

            if (!TryResolveCanvasChild(source, target, edge.Lane, out var parentId, out var childId))
            {
                continue;
            }

            if (!childMap.TryGetValue(parentId, out var children))
            {
                children = new HashSet<string>(StringComparer.Ordinal);
                childMap[parentId] = children;
            }

            children.Add(childId);
        }

        return childMap;
    }

    private static HashSet<string> CollectHiddenCanvasNodeIds(
        IReadOnlyList<UsageMapCanvasNodeItemViewModel> nodeItems,
        IReadOnlyDictionary<string, HashSet<string>> childMap)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var collapsedNode in nodeItems.Where(static node => node.IsCollapsed))
        {
            if (!childMap.TryGetValue(collapsedNode.Id, out var directChildren))
            {
                continue;
            }

            var queue = new Queue<string>(directChildren);
            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (!hidden.Add(currentId))
                {
                    continue;
                }

                if (childMap.TryGetValue(currentId, out var descendants))
                {
                    foreach (var descendantId in descendants)
                    {
                        queue.Enqueue(descendantId);
                    }
                }
            }
        }

        return hidden;
    }

    private static bool TryResolveCanvasChild(
        UsageMapCanvasNodeItemViewModel source,
        UsageMapCanvasNodeItemViewModel target,
        CanvasNodeLane lane,
        out string parentId,
        out string childId)
    {
        parentId = string.Empty;
        childId = string.Empty;

        switch (lane)
        {
            case CanvasNodeLane.Outbound:
                if (target.Left > source.Left)
                {
                    parentId = source.Id;
                    childId = target.Id;
                    return true;
                }

                return false;
            case CanvasNodeLane.Inbound:
                if (source.Left < target.Left)
                {
                    parentId = target.Id;
                    childId = source.Id;
                    return true;
                }

                return false;
            case CanvasNodeLane.Related:
                if (target.Top > source.Top || (Math.Abs(target.Top - source.Top) < 0.1d && target.Left > source.Left))
                {
                    parentId = source.Id;
                    childId = target.Id;
                    return true;
                }

                if (source.Top > target.Top || (Math.Abs(source.Top - target.Top) < 0.1d && source.Left > target.Left))
                {
                    parentId = target.Id;
                    childId = source.Id;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private sealed record EdgeRouteContext(
        CanvasEdgeViewModel Edge,
        UsageMapCanvasNodeItemViewModel Source,
        UsageMapCanvasNodeItemViewModel Target,
        AnchorSide SourceSide,
        AnchorSide TargetSide,
        double StartX,
        double StartY,
        double EndX,
        double EndY);
}
}
