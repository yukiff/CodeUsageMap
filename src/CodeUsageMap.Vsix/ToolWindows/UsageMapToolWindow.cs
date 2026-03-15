using System;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Vsix.Services;
using CodeUsageMap.Vsix.ViewModels;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PresentationUsageMapViewModel = CodeUsageMap.Contracts.Presentation.UsageMapViewModel;

namespace CodeUsageMap.Vsix.ToolWindows
{

public sealed class UsageMapToolWindow : ToolWindowPane
{
    private readonly UsageMapExportService _exportService;
    private readonly GraphCanvasViewModelBuilder _graphCanvasViewModelBuilder;
    private readonly UsageMapViewModel _viewModel;
    private UsageMapExportSnapshot? _exportSnapshot;

    public UsageMapToolWindow()
        : base(null)
    {
        Caption = "Code Usage Map";
        _exportService = new UsageMapExportService();
        _graphCanvasViewModelBuilder = new GraphCanvasViewModelBuilder();
        _viewModel = new UsageMapViewModel(new NavigationService());
        _viewModel.SetExportHandler(ExportAsync);
        Content = new UsageMapControl
        {
            DataContext = _viewModel,
        };
    }

    internal void Load(AnalyzeRequest request, AnalysisResult result, PresentationUsageMapViewModel model)
    {
        var canvasModel = _graphCanvasViewModelBuilder.Build(result);
        _exportSnapshot = new UsageMapExportSnapshot
        {
            Request = request,
            Result = result,
            ViewModel = model,
        };
        _viewModel.Load(model, canvasModel);
    }

    internal void BeginAnalysis(VisualStudioSymbolContext context, string statusMessage, CancellationTokenSource cancellation)
    {
        _exportSnapshot = null;
        _viewModel.BeginAnalysis(context, statusMessage, cancellation);
    }

    internal void ReportStatus(string statusMessage)
    {
        _viewModel.ReportStatus(statusMessage);
    }

    internal void MarkCanceled()
    {
        _viewModel.MarkCanceled();
    }

    internal void ShowError(string message)
    {
        _viewModel.ShowError(message);
    }

    internal AnalyzeOptions CreateAnalyzeOptions()
    {
        return _viewModel.CreateAnalyzeOptions();
    }

    internal void SetRefreshHandler(Func<AnalyzeOptions, Task> refreshHandler)
    {
        _viewModel.SetRefreshHandler(refreshHandler);
    }

    internal void SetRootSearchHandlers(
        Func<string, CancellationToken, Task<IReadOnlyList<UsageMapRootSearchResultItemViewModel>>> rootSearchHandler,
        Func<UsageMapRootSearchResultItemViewModel, Task> applyRootSearchResultHandler)
    {
        _viewModel.SetRootSearchHandlers(rootSearchHandler, applyRootSearchResultHandler);
    }

    internal void SetFollowCaretHandler(Func<bool, Task> followCaretHandler)
    {
        _viewModel.SetFollowCaretHandler(followCaretHandler);
    }

    internal void SetRerootHandler(Func<UsageMapCanvasNodeItemViewModel, Task> rerootHandler)
    {
        _viewModel.SetRerootHandler(rerootHandler);
    }

    private async Task ExportAsync(UsageMapExportFormat format)
    {
        if (_exportSnapshot is null)
        {
            _viewModel.ReportStatus("Nothing to export.");
            return;
        }

        try
        {
            var path = await _exportService.ExportAsync(_exportSnapshot, format, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _viewModel.ReportStatus($"Exported: {path}");
            }
        }
        catch (Exception ex)
        {
            _viewModel.ShowError(ex.Message);
        }
    }
}
}
