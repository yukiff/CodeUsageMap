using System;
using System.Threading;
using System.Threading.Tasks;
using CodeUsageMap.Vsix.ToolWindows;
using Microsoft.VisualStudio.Shell;

namespace CodeUsageMap.Vsix.Services
{

internal sealed class CaretFollowController
{
    private static readonly TimeSpan FollowCaretDebounce = TimeSpan.FromMilliseconds(300);
    private readonly UsageMapAnalysisCoordinator _analysisCoordinator;
    private readonly VisualStudioSymbolContextService _symbolContextService;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _followCancellation;
    private string? _lastSymbolKey;

    public CaretFollowController(
        UsageMapAnalysisCoordinator analysisCoordinator,
        VisualStudioSymbolContextService symbolContextService)
    {
        _analysisCoordinator = analysisCoordinator;
        _symbolContextService = symbolContextService;
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            _followCancellation?.Cancel();
            _followCancellation = null;
        }
    }

    public void ResetTrackedSymbol(string? symbolKey)
    {
        lock (_syncRoot)
        {
            _lastSymbolKey = symbolKey;
        }
    }

    public Task UpdateAsync(bool enabled, UsageMapToolWindow toolWindow, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            Stop();
            return Task.CompletedTask;
        }

        lock (_syncRoot)
        {
            if (_followCancellation is not null)
            {
                return Task.CompletedTask;
            }

            _followCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = RunLoopAsync(toolWindow, _followCancellation.Token);
            return Task.CompletedTask;
        }
    }

    private async Task RunLoopAsync(UsageMapToolWindow toolWindow, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(FollowCaretDebounce);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var context = await _symbolContextService.TryGetCurrentContextAsync(cancellationToken);
                if (context is null)
                {
                    continue;
                }

                if (IsSameSymbol(context.SymbolKey))
                {
                    continue;
                }

                ResetTrackedSymbol(context.SymbolKey);
                await _analysisCoordinator.AnalyzeAsync(
                    context,
                    toolWindow,
                    toolWindow.CreateAnalyzeOptions(),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_syncRoot)
            {
                _followCancellation?.Dispose();
                _followCancellation = null;
            }
        }
    }

    private bool IsSameSymbol(string symbolKey)
    {
        lock (_syncRoot)
        {
            return string.Equals(_lastSymbolKey, symbolKey, StringComparison.Ordinal);
        }
    }
}
}
