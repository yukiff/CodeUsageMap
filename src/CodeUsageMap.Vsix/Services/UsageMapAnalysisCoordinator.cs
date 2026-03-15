using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Vsix.ToolWindows;
using Microsoft.VisualStudio.Shell;

namespace CodeUsageMap.Vsix.Services;

internal sealed class UsageMapAnalysisCoordinator
{
    private readonly SemaphoreSlim _analysisGate = new(1, 1);
    private readonly object _syncRoot = new();
    private readonly CSharpUsageAnalyzer _analyzer;
    private readonly UsageMapViewModelBuilder _viewModelBuilder;
    private CancellationTokenSource? _activeAnalysisCancellation;
    private int _activeAnalysisId;

    public UsageMapAnalysisCoordinator(
        CSharpUsageAnalyzer analyzer,
        UsageMapViewModelBuilder viewModelBuilder)
    {
        _analyzer = analyzer;
        _viewModelBuilder = viewModelBuilder;
    }

    public async Task AnalyzeAsync(
        VisualStudioSymbolContext context,
        UsageMapToolWindow toolWindow,
        AnalyzeOptions options,
        CancellationToken cancellationToken)
    {
        var (analysisId, analysisCancellation) = StartAnalysis(cancellationToken);
        var gateEntered = false;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _analysisGate.WaitAsync(analysisCancellation.Token);
            gateEntered = true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(analysisCancellation.Token);
            if (!IsCurrentAnalysis(analysisId))
            {
                return;
            }

            toolWindow.BeginAnalysis(context, FormatStatus("Resolving symbol...", stopwatch), analysisCancellation);
            var progress = new Progress<AnalysisProgressUpdate>(update => toolWindow.ReportStatus(FormatProgress(update, stopwatch)));

            var result = await Task.Run(
                () => _analyzer.AnalyzeAsync(
                    new AnalyzeRequest
                    {
                        SolutionPath = context.SolutionPath,
                        SymbolName = context.SymbolName,
                        Options = NormalizeOptions(options),
                        Progress = progress,
                    },
                    analysisCancellation.Token),
                analysisCancellation.Token).ConfigureAwait(false);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(analysisCancellation.Token);
            if (!IsCurrentAnalysis(analysisId))
            {
                return;
            }

            toolWindow.ReportStatus(FormatStatus("Building view model...", stopwatch));
            var viewModel = _viewModelBuilder.Build(result);
            var request = new AnalyzeRequest
            {
                SolutionPath = context.SolutionPath,
                SymbolName = context.SymbolName,
                Options = NormalizeOptions(options),
            };

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(analysisCancellation.Token);
            if (!IsCurrentAnalysis(analysisId))
            {
                return;
            }

            toolWindow.Load(request, result, viewModel);
            toolWindow.ReportStatus(FormatStatus("Analysis completed.", stopwatch));
        }
        catch (OperationCanceledException)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (IsCurrentAnalysis(analysisId))
            {
                toolWindow.MarkCanceled();
            }
        }
        catch (Exception ex)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (IsCurrentAnalysis(analysisId))
            {
                toolWindow.ShowError(ex.Message);
            }
        }
        finally
        {
            ClearAnalysis(analysisId, analysisCancellation);
            if (gateEntered)
            {
                _analysisGate.Release();
            }
        }
    }

    private (int AnalysisId, CancellationTokenSource Cancellation) StartAnalysis(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _activeAnalysisId++;
            _activeAnalysisCancellation?.Cancel();

            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeAnalysisCancellation = linkedCancellation;
            return (_activeAnalysisId, linkedCancellation);
        }
    }

    private bool IsCurrentAnalysis(int analysisId)
    {
        lock (_syncRoot)
        {
            return _activeAnalysisId == analysisId;
        }
    }

    private static AnalyzeOptions NormalizeOptions(AnalyzeOptions options)
    {
        return new AnalyzeOptions
        {
            Depth = options.Depth <= 0 ? 1 : options.Depth,
            SymbolIndex = options.SymbolIndex,
            ExcludeGenerated = options.ExcludeGenerated,
            ExcludeTests = options.ExcludeTests,
            WorkspaceLoader = string.IsNullOrWhiteSpace(options.WorkspaceLoader) ? "msbuild" : options.WorkspaceLoader,
        };
    }

    private void ClearAnalysis(int analysisId, CancellationTokenSource cancellation)
    {
        lock (_syncRoot)
        {
            if (_activeAnalysisId == analysisId && ReferenceEquals(_activeAnalysisCancellation, cancellation))
            {
                _activeAnalysisCancellation = null;
            }
        }

        cancellation.Dispose();
    }

    private static string FormatProgress(AnalysisProgressUpdate update, Stopwatch stopwatch)
    {
        return update.Stage switch
        {
            AnalysisProgressStage.CollectingReferences => FormatStatus(
                $"Collecting references... depth {update.Depth ?? 1}, expanded {update.ExpandedSymbols ?? 0}",
                stopwatch),
            AnalysisProgressStage.CollectingImplementations => FormatStatus(
                $"Collecting implementations... depth {update.Depth ?? 1}, expanded {update.ExpandedSymbols ?? 0}",
                stopwatch),
            AnalysisProgressStage.CollectingEvents => FormatStatus(
                $"Collecting events... depth {update.Depth ?? 1}, expanded {update.ExpandedSymbols ?? 0}",
                stopwatch),
            AnalysisProgressStage.ResolvingExpansion => FormatStatus(
                $"Resolving expansion... depth {update.Depth ?? 1}, expanded {update.ExpandedSymbols ?? 0}",
                stopwatch),
            _ => FormatStatus(update.Message, stopwatch),
        };
    }

    private static string FormatStatus(string message, Stopwatch stopwatch)
    {
        return $"{message} ({stopwatch.ElapsedMilliseconds} ms)";
    }
}
