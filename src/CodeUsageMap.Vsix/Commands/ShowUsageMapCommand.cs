using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using CodeUsageMap.Core;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Vsix.Services;
using CodeUsageMap.Vsix.ToolWindows;
using CodeUsageMap.Vsix.ViewModels;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodeUsageMap.Vsix.Commands;

internal sealed class ShowUsageMapCommand
{
    private readonly AsyncPackage _package;
    private readonly UsageMapAnalysisCoordinator _analysisCoordinator;
    private readonly CaretFollowController _caretFollowController;
    private readonly VisualStudioSymbolContextService _symbolContextService;

    private ShowUsageMapCommand(
        AsyncPackage package,
        UsageMapAnalysisCoordinator analysisCoordinator,
        CaretFollowController caretFollowController,
        VisualStudioSymbolContextService symbolContextService)
    {
        _package = package;
        _analysisCoordinator = analysisCoordinator;
        _caretFollowController = caretFollowController;
        _symbolContextService = symbolContextService;
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        if (commandService is null)
        {
            return;
        }

        var analysisCoordinator = new UsageMapAnalysisCoordinator(
            new CSharpUsageAnalyzer(),
            new UsageMapViewModelBuilder());
        var symbolContextService = new VisualStudioSymbolContextService(package);
        var instance = new ShowUsageMapCommand(
            package,
            analysisCoordinator,
            new CaretFollowController(analysisCoordinator, symbolContextService),
            symbolContextService);

        var menuCommand = new MenuCommand(instance.Execute, new CommandID(CommandIds.CommandSet, CommandIds.ShowUsageMap));
        commandService.AddCommand(menuCommand);
    }

    private void Execute(object? sender, EventArgs e)
    {
        _package.JoinableTaskFactory.RunAsync(() => ExecuteAsync());
    }

    private async Task ExecuteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        try
        {
            var context = await _symbolContextService.TryGetCurrentContextAsync(_package.DisposalToken);
            if (context is null)
            {
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "The current symbol could not be resolved.",
                    "CodeUsageMap",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var activeContext = context;

            var toolWindow = await _package.ShowToolWindowAsync(typeof(UsageMapToolWindow), 0, true, _package.DisposalToken) as UsageMapToolWindow;
            if (toolWindow is null)
            {
                throw new InvalidOperationException("Failed to create UsageMapToolWindow.");
            }

            toolWindow.SetRefreshHandler(async options =>
            {
                var refreshContext = await ResolveRefreshContextAsync(activeContext);
                activeContext = refreshContext;
                _caretFollowController.ResetTrackedSymbol(refreshContext.SymbolKey);
                await _analysisCoordinator.AnalyzeAsync(refreshContext, toolWindow, options, _package.DisposalToken);
            });
            toolWindow.SetRootSearchHandlers(
                async (query, cancellationToken) =>
                {
                    var results = await _symbolContextService.SearchSymbolsAsync(query, maxResults: 24, cancellationToken);
                    return results.Select(result => new UsageMapRootSearchResultItemViewModel
                    {
                        SolutionPath = result.SolutionPath,
                        SymbolName = result.SymbolName,
                        DisplayName = result.DisplayName,
                        SymbolKey = result.SymbolKey,
                        Kind = result.Kind,
                        ProjectName = result.ProjectName,
                        FilePath = result.FilePath,
                        LineNumber = result.LineNumber,
                    }).ToArray();
                },
                async result =>
                {
                    var searchContext = new VisualStudioSymbolContext
                    {
                        SolutionPath = result.SolutionPath,
                        SymbolName = result.SymbolName,
                        DisplayName = result.DisplayName,
                        SymbolKey = result.SymbolKey,
                        Kind = result.Kind,
                        ProjectName = result.ProjectName,
                        FilePath = result.FilePath,
                        LineNumber = result.LineNumber,
                    };
                    activeContext = searchContext;
                    _caretFollowController.ResetTrackedSymbol(searchContext.SymbolKey);
                    await _analysisCoordinator.AnalyzeAsync(searchContext, toolWindow, toolWindow.CreateAnalyzeOptions(), _package.DisposalToken);
                });
            toolWindow.SetFollowCaretHandler(enabled => _caretFollowController.UpdateAsync(enabled, toolWindow, _package.DisposalToken));
            toolWindow.SetRerootHandler(async node =>
            {
                var rerootContext = new VisualStudioSymbolContext
                {
                    SolutionPath = activeContext.SolutionPath,
                    SymbolName = node.SymbolKey,
                    DisplayName = node.DisplayName,
                    SymbolKey = node.SymbolKey,
                    Kind = node.Kind,
                    ProjectName = node.ProjectName,
                    FilePath = node.FilePath,
                    LineNumber = node.LineNumber,
                };
                activeContext = rerootContext;
                _caretFollowController.ResetTrackedSymbol(rerootContext.SymbolKey);
                await _analysisCoordinator.AnalyzeAsync(rerootContext, toolWindow, toolWindow.CreateAnalyzeOptions(), _package.DisposalToken);
            });
            _caretFollowController.ResetTrackedSymbol(context.SymbolKey);
            await _analysisCoordinator.AnalyzeAsync(context, toolWindow, toolWindow.CreateAnalyzeOptions(), _package.DisposalToken);
        }
        catch (Exception ex)
        {
            VsShellUtilities.ShowMessageBox(
                _package,
                ex.Message,
                "CodeUsageMap",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }

    private async Task<VisualStudioSymbolContext> ResolveRefreshContextAsync(VisualStudioSymbolContext fallbackContext)
    {
        var currentContext = await _symbolContextService.TryGetCurrentContextAsync(_package.DisposalToken);
        return currentContext ?? fallbackContext;
    }
}
