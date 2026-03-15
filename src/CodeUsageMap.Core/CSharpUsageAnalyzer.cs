using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;
using CodeUsageMap.Core.Analysis;
using CodeUsageMap.Core.Compatibility;
using CodeUsageMap.Core.Di;
using CodeUsageMap.Core.Events;
using CodeUsageMap.Core.Implementations;
using CodeUsageMap.Core.References;
using CodeUsageMap.Core.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace CodeUsageMap.Core
{

public sealed class CSharpUsageAnalyzer : IUsageAnalyzer
{
    private const int MaxExpandedSymbolsPerAnalysis = 200;
    private const int MaxExpansionCandidatesPerSymbol = 64;
    private readonly RoslynSymbolResolver _symbolResolver;
    private readonly RoslynEventUsageCollector _eventUsageCollector;
    private readonly RoslynDiRegistrationAnalyzer _diRegistrationAnalyzer;
    private readonly RoslynImplementationCollector _implementationCollector;
    private readonly RoslynOutgoingCallCollector _outgoingCallCollector;
    private readonly RoslynReferenceCollector _referenceCollector;
    private readonly RoslynWorkspaceLoader _workspaceLoader;
    private readonly AnalysisResultCache _resultCache;

    public CSharpUsageAnalyzer()
        : this(
            new RoslynSymbolResolver(),
            new RoslynEventUsageCollector(),
            new RoslynDiRegistrationAnalyzer(),
            new RoslynImplementationCollector(),
            new RoslynOutgoingCallCollector(),
            new RoslynReferenceCollector(),
            new RoslynWorkspaceLoader(),
            new AnalysisResultCache())
    {
    }

    internal CSharpUsageAnalyzer(
        RoslynSymbolResolver symbolResolver,
        RoslynEventUsageCollector eventUsageCollector,
        RoslynDiRegistrationAnalyzer diRegistrationAnalyzer,
        RoslynImplementationCollector implementationCollector,
        RoslynOutgoingCallCollector outgoingCallCollector,
        RoslynReferenceCollector referenceCollector,
        RoslynWorkspaceLoader workspaceLoader,
        AnalysisResultCache resultCache)
    {
        _symbolResolver = symbolResolver;
        _eventUsageCollector = eventUsageCollector;
        _diRegistrationAnalyzer = diRegistrationAnalyzer;
        _implementationCollector = implementationCollector;
        _outgoingCallCollector = outgoingCallCollector;
        _referenceCollector = referenceCollector;
        _workspaceLoader = workspaceLoader;
        _resultCache = resultCache;
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        Guard.NotNull(request, nameof(request));

        var diagnostics = new List<AnalysisDiagnostic>();
        var graph = new UsageGraph();

        if (!File.Exists(request.SolutionPath))
        {
            diagnostics.Add(new AnalysisDiagnostic
            {
                Code = "solution_not_found",
                Message = $"Solution file was not found: {request.SolutionPath}",
                Confidence = AnalysisConfidence.Confirmed,
            });

            var result = new AnalysisResult
            {
                Graph = graph,
                SymbolResolution = new SymbolResolutionInfo
                {
                    Status = SymbolResolutionStatus.NotFound,
                    RequestedSymbolName = request.SymbolName,
                    RequestedSymbolIndex = request.Options.SymbolIndex,
                },
                Diagnostics = diagnostics,
            };
        }

        if (string.IsNullOrWhiteSpace(request.SymbolName))
        {
            diagnostics.Add(new AnalysisDiagnostic
            {
                Code = "symbol_required",
                Message = "A symbol name is required.",
                Confidence = AnalysisConfidence.Confirmed,
            });

            var result = new AnalysisResult
            {
                Graph = graph,
                SymbolResolution = new SymbolResolutionInfo
                {
                    Status = SymbolResolutionStatus.NotFound,
                    RequestedSymbolName = request.SymbolName,
                    RequestedSymbolIndex = request.Options.SymbolIndex,
                },
                Diagnostics = diagnostics,
            };
        }

        if (_resultCache.TryGet(request, out var cachedResult))
        {
            ReportProgress(AnalysisProgressStage.Completed, "Analysis completed (cache hit).");
            return AppendCacheHitDiagnostic(cachedResult);
        }

        ReportProgress(AnalysisProgressStage.LoadingSolution, "Loading solution...");
        using var loadedSolution = await _workspaceLoader.LoadAsync(
            request.SolutionPath,
            request.Options.WorkspaceLoader,
            cancellationToken);

        if (request.Options.ExcludeTests)
        {
            diagnostics.Add(new AnalysisDiagnostic
            {
                Code = "filter_exclude_tests_applied",
                Message = "ExcludeTests filter was applied. Test-like projects and files were omitted from analysis.",
                Confidence = AnalysisConfidence.High,
            });
        }

        if (request.Options.ExcludeGenerated)
        {
            diagnostics.Add(new AnalysisDiagnostic
            {
                Code = "filter_exclude_generated_applied",
                Message = "ExcludeGenerated filter was applied. Generated files and common generated paths were omitted from analysis.",
                Confidence = AnalysisConfidence.High,
            });
        }

        ReportProgress(AnalysisProgressStage.ResolvingSymbol, "Resolving symbol...");
        var resolution = await _symbolResolver.ResolveAsync(
            loadedSolution,
            request.SymbolName,
            request.Options.SymbolIndex,
            cancellationToken);
        if (resolution is null)
        {
            var fallbackSymbol = _symbolResolver.CreateFallback(request.SymbolName);
            graph.Nodes.Add(new GraphNode
            {
                Id = fallbackSymbol.SymbolKey,
                DisplayName = fallbackSymbol.DisplayName,
                Kind = fallbackSymbol.Kind,
                SymbolKey = fallbackSymbol.SymbolKey,
                FilePath = request.SolutionPath,
                ProjectName = Path.GetFileNameWithoutExtension(request.SolutionPath),
            });

            diagnostics.Add(new AnalysisDiagnostic
            {
                Code = "symbol_not_found",
                Message = $"The symbol '{request.SymbolName}' was not resolved in the solution. A fallback root node was emitted.",
                Confidence = AnalysisConfidence.High,
            });

            var result = new AnalysisResult
            {
                Graph = graph,
                SymbolResolution = new SymbolResolutionInfo
                {
                    Status = SymbolResolutionStatus.NotFound,
                    RequestedSymbolName = request.SymbolName,
                    RequestedSymbolIndex = request.Options.SymbolIndex,
                },
                Diagnostics = diagnostics,
            };
            _resultCache.Store(request, result);
            return result;
        }

        if (resolution.Resolution.Status is SymbolResolutionStatus.Ambiguous or SymbolResolutionStatus.InvalidSelection)
        {
            diagnostics.Add(new AnalysisDiagnostic
            {
                Code = resolution.Resolution.Status == SymbolResolutionStatus.Ambiguous
                    ? "symbol_ambiguous"
                    : "symbol_index_invalid",
                Message = resolution.Resolution.Status == SymbolResolutionStatus.Ambiguous
                    ? $"The symbol '{request.SymbolName}' matched {resolution.Resolution.Candidates.Count} candidates. Specify SymbolIndex to select one."
                    : $"The requested symbol index '{request.Options.SymbolIndex}' is invalid for '{request.SymbolName}'.",
                Confidence = AnalysisConfidence.Confirmed,
            });

            var result = new AnalysisResult
            {
                Graph = graph,
                SymbolResolution = resolution.Resolution,
                Diagnostics = diagnostics,
            };
            _resultCache.Store(request, result);
            return result;
        }

        var resolvedSymbol = resolution.Symbol;
        var roslynSymbol = resolution.RoslynSymbol;
        var primaryLocation = roslynSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
        var primarySyntaxReference = roslynSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        int? syntaxReferenceLineNumber = primarySyntaxReference is null
            ? null
            : primarySyntaxReference.SyntaxTree.GetLineSpan(primarySyntaxReference.Span).StartLinePosition.Line + 1;
        var primaryFilePath = primaryLocation?.SourceTree?.FilePath
            ?? primarySyntaxReference?.SyntaxTree?.FilePath
            ?? request.SolutionPath;
        var primaryLineNumber = primaryLocation?.GetLineSpan().StartLinePosition.Line + 1
            ?? syntaxReferenceLineNumber;
        graph.Nodes.Add(new GraphNode
        {
            Id = resolvedSymbol.SymbolKey,
            DisplayName = resolvedSymbol.DisplayName,
            Kind = resolvedSymbol.Kind,
            SymbolKey = resolvedSymbol.SymbolKey,
            FilePath = primaryFilePath,
            ProjectName = Path.GetFileNameWithoutExtension(request.SolutionPath),
            LineNumber = primaryLineNumber,
            Properties = CreateProperties(
                ("symbolKey", resolvedSymbol.SymbolKey),
                ("projectName", Path.GetFileNameWithoutExtension(request.SolutionPath)),
                ("filePath", primaryFilePath),
                ("lineNumber", primaryLineNumber),
                ("namespaceName", roslynSymbol.ContainingNamespace?.ToDisplayString()),
                ("accessibility", resolvedSymbol.Accessibility),
                ("nodeKind", resolvedSymbol.Kind.ToString())),
        });

        var solution = loadedSolution.Solution;
        var projectName = roslynSymbol.ContainingAssembly?.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            graph.Nodes[0] = new GraphNode
            {
                Id = resolvedSymbol.SymbolKey,
                DisplayName = resolvedSymbol.DisplayName,
                Kind = resolvedSymbol.Kind,
                SymbolKey = resolvedSymbol.SymbolKey,
                FilePath = primaryFilePath,
                ProjectName = projectName,
                LineNumber = primaryLineNumber,
                Properties = CreateProperties(
                    ("symbolKey", resolvedSymbol.SymbolKey),
                    ("projectName", projectName),
                    ("filePath", primaryFilePath),
                    ("lineNumber", primaryLineNumber),
                    ("namespaceName", roslynSymbol.ContainingNamespace?.ToDisplayString()),
                    ("accessibility", resolvedSymbol.Accessibility),
                    ("nodeKind", resolvedSymbol.Kind.ToString())),
            };
        }

        var nodeIds = new HashSet<string>(graph.Nodes.Select(static node => node.Id), StringComparer.Ordinal);
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        var binaryReferenceDiagnostics = new HashSet<string>(StringComparer.Ordinal);
        var visitedSymbols = new HashSet<string>(StringComparer.Ordinal);
        var queuedSymbols = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<(ISymbol Symbol, ResolvedSymbol ResolvedSymbol, int Depth)>();
        var expandedSymbols = 0;
        var maxDepth = Math.Max(request.Options.Depth, 1);

        Enqueue(roslynSymbol, resolvedSymbol, 1);

        while (frontier.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (expandedSymbols >= MaxExpandedSymbolsPerAnalysis)
            {
                diagnostics.Add(new AnalysisDiagnostic
                {
                    Code = "depth_global_symbol_limit_reached",
                    Message = $"Depth expansion stopped after {MaxExpandedSymbolsPerAnalysis} symbols to preserve responsiveness.",
                    Confidence = AnalysisConfidence.High,
                });
                break;
            }

            var current = frontier.Dequeue();
            if (!visitedSymbols.Add(CreateTraversalKey(current.Symbol)))
            {
                continue;
            }

            expandedSymbols++;

            ReportProgress(
                AnalysisProgressStage.CollectingReferences,
                $"Collecting references... depth {current.Depth}",
                current.ResolvedSymbol.DisplayName,
                current.Depth,
                expandedSymbols);
            var references = await _referenceCollector.CollectAsync(current.Symbol, solution, request.Options, cancellationToken);

            ReportProgress(
                AnalysisProgressStage.CollectingImplementations,
                $"Collecting implementations... depth {current.Depth}",
                current.ResolvedSymbol.DisplayName,
                current.Depth,
                expandedSymbols);
            var implementations = await _implementationCollector.CollectAsync(current.Symbol, solution, request.Options, cancellationToken);

            ReportProgress(
                AnalysisProgressStage.CollectingDiRegistrations,
                $"Collecting DI registrations... depth {current.Depth}",
                current.ResolvedSymbol.DisplayName,
                current.Depth,
                expandedSymbols);
            var diRegistrations = await _diRegistrationAnalyzer.CollectAsync(current.Symbol, solution, request.Options, cancellationToken);

            ReportProgress(
                AnalysisProgressStage.CollectingEvents,
                $"Collecting events... depth {current.Depth}",
                current.ResolvedSymbol.DisplayName,
                current.Depth,
                expandedSymbols);
            var eventUsages = await _eventUsageCollector.CollectAsync(current.Symbol, solution, request.Options, cancellationToken);
            var outgoingCalls = await _outgoingCallCollector.CollectAsync(current.Symbol, solution, request.Options, cancellationToken);

            AddReferenceNodesAndEdges(current.ResolvedSymbol, references);
            AddImplementationNodesAndEdges(current.ResolvedSymbol, implementations);
            AddDiRegistrationNodesAndEdges(current.ResolvedSymbol, diRegistrations);
            AddEventUsageNodesAndEdges(eventUsages);
            AddOutgoingCallNodesAndEdges(current.ResolvedSymbol, outgoingCalls);

            if (current.Depth >= maxDepth)
            {
                continue;
            }

            ReportProgress(
                AnalysisProgressStage.ResolvingExpansion,
                $"Resolving expansion... depth {current.Depth + 1}",
                current.ResolvedSymbol.DisplayName,
                current.Depth + 1,
                expandedSymbols);
            foreach (var nextSymbol in await ResolveExpansionSymbolsAsync(
                current.Symbol,
                current.ResolvedSymbol,
                references,
                implementations,
                eventUsages,
                outgoingCalls,
                loadedSolution,
                diagnostics,
                cancellationToken))
            {
                Enqueue(nextSymbol.RoslynSymbol, nextSymbol.Symbol, current.Depth + 1);
            }
        }

        diagnostics.Add(new AnalysisDiagnostic
        {
            Code = "reference_analysis_complete",
            Message = $"Resolved '{resolvedSymbol.DisplayName}', expanded {expandedSymbols} symbols across depth {maxDepth}, and used workspace loader '{ResolveWorkspaceLoaderLabel(request.Options.WorkspaceLoader)}'.",
            Confidence = AnalysisConfidence.High,
        });

        ReportProgress(
            AnalysisProgressStage.Completed,
            "Analysis completed.",
            resolvedSymbol.DisplayName,
            maxDepth,
            expandedSymbols);

        var completedResult = new AnalysisResult
        {
            Graph = graph,
            SymbolResolution = resolution.Resolution,
            Diagnostics = diagnostics,
        };
        _resultCache.Store(request, completedResult);
        return completedResult;

        void AddNodeIfMissing(GraphNode node)
        {
            if (nodeIds.Add(node.Id))
            {
                graph.Nodes.Add(node);
            }
        }

        void AddEdgeIfMissing(GraphEdge edge)
        {
            var edgeKey = $"{edge.SourceId}|{edge.TargetId}|{edge.Kind}|{edge.Label}";
            if (edgeKeys.Add(edgeKey))
            {
                graph.Edges.Add(edge);
            }
        }

        void Enqueue(ISymbol symbol, ResolvedSymbol symbolInfo, int depth)
        {
            var traversalKey = CreateTraversalKey(symbol);
            if (visitedSymbols.Contains(traversalKey) || !queuedSymbols.Add(traversalKey))
            {
                return;
            }

            frontier.Enqueue((symbol, symbolInfo, depth));
        }

        void AddReferenceNodesAndEdges(
            ResolvedSymbol targetSymbol,
            IReadOnlyList<ReferenceInfo> references)
        {
            foreach (var reference in references)
            {
                var referenceNodeId = $"{reference.ContainingSymbol}@{reference.FilePath}:{reference.LineNumber}";
                AddNodeIfMissing(new GraphNode
                {
                    Id = referenceNodeId,
                    DisplayName = reference.ContainingSymbol,
                    Kind = reference.ContainingSymbolKind,
                    ProjectName = reference.ProjectName,
                    FilePath = reference.FilePath,
                    LineNumber = reference.LineNumber,
                    SymbolKey = referenceNodeId,
                    Properties = CreateProperties(
                        ("projectName", reference.ProjectName),
                        ("namespaceName", reference.NamespaceName),
                        ("accessibility", reference.Accessibility),
                        ("filePath", reference.FilePath),
                        ("lineNumber", reference.LineNumber),
                        ("nodeKind", reference.ContainingSymbolKind.ToString())),
                });

                AddEdgeIfMissing(new GraphEdge
                {
                    SourceId = referenceNodeId,
                    TargetId = targetSymbol.SymbolKey,
                    Kind = reference.Kind,
                    Label = reference.Kind.ToString(),
                    Confidence = reference.Kind is EdgeKind.EventSubscription or EdgeKind.EventUnsubscription ? 0.9d : 1.0d,
                    Properties = CreateProperties(
                        ("projectName", reference.ProjectName),
                        ("namespaceName", reference.NamespaceName),
                        ("accessibility", reference.Accessibility),
                        ("filePath", reference.FilePath),
                        ("lineNumber", reference.LineNumber),
                        ("containingSymbol", reference.ContainingSymbol),
                        ("containingSymbolKind", reference.ContainingSymbolKind.ToString()),
                        ("syntaxKind", reference.SyntaxKind),
                        ("referenceText", reference.ReferenceText)),
                });
            }
        }

        void AddImplementationNodesAndEdges(
            ResolvedSymbol sourceSymbol,
            IReadOnlyList<ImplementationInfo> implementations)
        {
            foreach (var implementation in implementations)
            {
                AddNodeIfMissing(new GraphNode
                {
                    Id = implementation.SymbolKey,
                    DisplayName = implementation.DisplayName,
                    Kind = implementation.NodeKind,
                    ProjectName = implementation.ProjectName,
                    FilePath = implementation.FilePath,
                    LineNumber = implementation.LineNumber,
                    SymbolKey = implementation.SymbolKey,
                    Properties = CreateProperties(
                        ("projectName", implementation.ProjectName),
                        ("namespaceName", implementation.NamespaceName),
                        ("accessibility", implementation.Accessibility),
                        ("filePath", implementation.FilePath),
                        ("lineNumber", implementation.LineNumber),
                        ("nodeKind", implementation.NodeKind.ToString()),
                        ("containingTypeName", implementation.ContainingTypeName)),
                });

                AddEdgeIfMissing(new GraphEdge
                {
                    SourceId = sourceSymbol.SymbolKey,
                    TargetId = implementation.SymbolKey,
                    Kind = implementation.Kind,
                    Label = implementation.Kind.ToString(),
                    Confidence = implementation.Kind == EdgeKind.Overrides ? 0.95d : 1.0d,
                    Properties = CreateProperties(
                        ("projectName", implementation.ProjectName),
                        ("namespaceName", implementation.NamespaceName),
                        ("accessibility", implementation.Accessibility),
                        ("filePath", implementation.FilePath),
                        ("lineNumber", implementation.LineNumber),
                        ("implementationDisplayName", implementation.DisplayName),
                        ("containingTypeName", implementation.ContainingTypeName),
                        ("isOverride", implementation.IsOverride)),
                });
            }
        }

        void AddEventUsageNodesAndEdges(IReadOnlyList<EventUsageInfo> eventUsages)
        {
            foreach (var eventUsage in eventUsages)
            {
                AddNodeIfMissing(new GraphNode
                {
                    Id = eventUsage.ContainingSymbolId,
                    DisplayName = eventUsage.ContainingSymbolDisplayName,
                    Kind = eventUsage.ContainingSymbolKind,
                    ProjectName = eventUsage.ProjectName,
                    FilePath = eventUsage.FilePath,
                    LineNumber = eventUsage.LineNumber,
                    SymbolKey = eventUsage.ContainingSymbolId,
                    Properties = CreateProperties(
                        ("projectName", eventUsage.ProjectName),
                        ("namespaceName", eventUsage.NamespaceName),
                        ("accessibility", eventUsage.Accessibility),
                        ("filePath", eventUsage.FilePath),
                        ("lineNumber", eventUsage.LineNumber),
                        ("nodeKind", eventUsage.ContainingSymbolKind.ToString())),
                });

                AddNodeIfMissing(new GraphNode
                {
                    Id = eventUsage.EventSymbolId,
                    DisplayName = eventUsage.EventName,
                    Kind = NodeKind.Event,
                    ProjectName = eventUsage.ProjectName,
                    FilePath = eventUsage.FilePath,
                    LineNumber = eventUsage.LineNumber,
                    SymbolKey = eventUsage.EventSymbolId,
                    Properties = CreateProperties(
                        ("projectName", eventUsage.ProjectName),
                        ("namespaceName", eventUsage.NamespaceName),
                        ("accessibility", eventUsage.Accessibility),
                        ("filePath", eventUsage.FilePath),
                        ("lineNumber", eventUsage.LineNumber),
                        ("publisherTypeName", eventUsage.PublisherTypeName),
                        ("eventName", eventUsage.EventName)),
                });

                if (!string.IsNullOrWhiteSpace(eventUsage.HandlerSymbolId))
                {
                    AddNodeIfMissing(new GraphNode
                    {
                        Id = eventUsage.HandlerSymbolId,
                        DisplayName = eventUsage.HandlerName,
                        Kind = eventUsage.HandlerKind,
                        ProjectName = eventUsage.ProjectName,
                        FilePath = eventUsage.FilePath,
                        LineNumber = eventUsage.LineNumber,
                        SymbolKey = eventUsage.HandlerSymbolId,
                        Properties = CreateProperties(
                            ("projectName", eventUsage.ProjectName),
                            ("namespaceName", eventUsage.NamespaceName),
                            ("accessibility", eventUsage.Accessibility),
                            ("filePath", eventUsage.FilePath),
                            ("lineNumber", eventUsage.LineNumber),
                            ("handlerName", eventUsage.HandlerName),
                            ("handlerKind", eventUsage.HandlerKind.ToString())),
                    });
                }

                if (eventUsage.Kind is EdgeKind.EventSubscription or EdgeKind.EventUnsubscription)
                {
                    AddEdgeIfMissing(new GraphEdge
                    {
                        SourceId = eventUsage.ContainingSymbolId,
                        TargetId = eventUsage.EventSymbolId,
                        Kind = EdgeKind.ContainsSubscription,
                        Label = EdgeKind.ContainsSubscription.ToString(),
                        Confidence = 1.0d,
                        Properties = CreateProperties(
                            ("eventName", eventUsage.EventName),
                            ("publisherTypeName", eventUsage.PublisherTypeName),
                            ("namespaceName", eventUsage.NamespaceName),
                            ("accessibility", eventUsage.Accessibility),
                            ("projectName", eventUsage.ProjectName),
                            ("filePath", eventUsage.FilePath),
                            ("lineNumber", eventUsage.LineNumber)),
                    });

                    if (!string.IsNullOrWhiteSpace(eventUsage.HandlerSymbolId))
                    {
                        AddEdgeIfMissing(new GraphEdge
                        {
                            SourceId = eventUsage.ContainingSymbolId,
                            TargetId = eventUsage.HandlerSymbolId,
                            Kind = EdgeKind.EventHandlerTarget,
                            Label = EdgeKind.EventHandlerTarget.ToString(),
                            Confidence = ToConfidenceScore(eventUsage.Confidence),
                            Properties = CreateProperties(
                                ("eventName", eventUsage.EventName),
                                ("publisherTypeName", eventUsage.PublisherTypeName),
                                ("namespaceName", eventUsage.NamespaceName),
                                ("accessibility", eventUsage.Accessibility),
                                ("handlerName", eventUsage.HandlerName),
                                ("handlerKind", eventUsage.HandlerKind.ToString()),
                                ("confidence", eventUsage.Confidence.ToString()),
                                ("isUnsubscribed", eventUsage.IsUnsubscribed)),
                        });

                        AddEdgeIfMissing(new GraphEdge
                        {
                            SourceId = eventUsage.EventSymbolId,
                            TargetId = eventUsage.HandlerSymbolId,
                            Kind = eventUsage.Kind,
                            Label = eventUsage.Kind.ToString(),
                            Confidence = ToConfidenceScore(eventUsage.Confidence),
                            Properties = CreateProperties(
                                ("eventName", eventUsage.EventName),
                                ("publisherTypeName", eventUsage.PublisherTypeName),
                                ("namespaceName", eventUsage.NamespaceName),
                                ("accessibility", eventUsage.Accessibility),
                                ("handlerName", eventUsage.HandlerName),
                                ("handlerKind", eventUsage.HandlerKind.ToString()),
                                ("confidence", eventUsage.Confidence.ToString()),
                                ("isUnsubscribed", eventUsage.IsUnsubscribed)),
                        });
                    }
                }

                if (eventUsage.Kind == EdgeKind.EventRaise)
                {
                    AddEdgeIfMissing(new GraphEdge
                    {
                        SourceId = eventUsage.ContainingSymbolId,
                        TargetId = eventUsage.EventSymbolId,
                        Kind = EdgeKind.EventRaise,
                        Label = EdgeKind.EventRaise.ToString(),
                        Confidence = ToConfidenceScore(eventUsage.Confidence),
                        Properties = CreateProperties(
                            ("eventName", eventUsage.EventName),
                            ("publisherTypeName", eventUsage.PublisherTypeName),
                            ("namespaceName", eventUsage.NamespaceName),
                            ("accessibility", eventUsage.Accessibility),
                            ("confidence", eventUsage.Confidence.ToString()),
                            ("projectName", eventUsage.ProjectName),
                            ("filePath", eventUsage.FilePath),
                            ("lineNumber", eventUsage.LineNumber)),
                    });
                }

                if (eventUsage.Kind == EdgeKind.EventDispatchEstimated &&
                    !string.IsNullOrWhiteSpace(eventUsage.HandlerSymbolId))
                {
                    AddEdgeIfMissing(new GraphEdge
                    {
                        SourceId = eventUsage.ContainingSymbolId,
                        TargetId = eventUsage.HandlerSymbolId,
                        Kind = EdgeKind.EventDispatchEstimated,
                        Label = eventUsage.Kind.ToString(),
                        Confidence = ToConfidenceScore(eventUsage.Confidence),
                        Properties = CreateProperties(
                            ("eventName", eventUsage.EventName),
                            ("publisherTypeName", eventUsage.PublisherTypeName),
                            ("namespaceName", eventUsage.NamespaceName),
                            ("accessibility", eventUsage.Accessibility),
                            ("handlerName", eventUsage.HandlerName),
                            ("handlerKind", eventUsage.HandlerKind.ToString()),
                            ("confidence", eventUsage.Confidence.ToString()),
                            ("isUnsubscribed", eventUsage.IsUnsubscribed)),
                    });
                }
            }
        }

        void AddDiRegistrationNodesAndEdges(
            ResolvedSymbol sourceSymbol,
            IReadOnlyList<DiRegistrationInfo> diRegistrations)
        {
            foreach (var registration in diRegistrations)
            {
                AddNodeIfMissing(new GraphNode
                {
                    Id = registration.RegistrationId,
                    DisplayName = registration.RegistrationDisplayName,
                    Kind = NodeKind.DiRegistration,
                    ProjectName = registration.ProjectName,
                    FilePath = registration.FilePath,
                    LineNumber = registration.LineNumber,
                    SymbolKey = registration.RegistrationId,
                    Properties = CreateProperties(
                        ("projectName", registration.ProjectName),
                        ("namespaceName", registration.NamespaceName),
                        ("filePath", registration.FilePath),
                        ("lineNumber", registration.LineNumber),
                        ("registrationText", registration.RegistrationText),
                        ("lifetime", registration.Lifetime),
                        ("registrationKind", registration.RegistrationKind),
                        ("nodeKind", NodeKind.DiRegistration.ToString())),
                });

                AddNodeIfMissing(new GraphNode
                {
                    Id = registration.ServiceSymbolId,
                    DisplayName = registration.ServiceDisplayName,
                    Kind = registration.ServiceKind,
                    ProjectName = registration.ProjectName,
                    FilePath = registration.FilePath,
                    LineNumber = registration.LineNumber,
                    SymbolKey = registration.ServiceSymbolId,
                    Properties = CreateProperties(
                        ("projectName", registration.ProjectName),
                        ("namespaceName", registration.NamespaceName),
                        ("filePath", registration.FilePath),
                        ("lineNumber", registration.LineNumber),
                        ("nodeKind", registration.ServiceKind.ToString()),
                        ("lifetime", registration.Lifetime)),
                });

                AddNodeIfMissing(new GraphNode
                {
                    Id = registration.ImplementationSymbolId,
                    DisplayName = registration.ImplementationDisplayName,
                    Kind = registration.ImplementationKind,
                    ProjectName = registration.ProjectName,
                    FilePath = registration.FilePath,
                    LineNumber = registration.LineNumber,
                    SymbolKey = registration.ImplementationSymbolId,
                    Properties = CreateProperties(
                        ("projectName", registration.ProjectName),
                        ("namespaceName", registration.NamespaceName),
                        ("filePath", registration.FilePath),
                        ("lineNumber", registration.LineNumber),
                        ("nodeKind", registration.ImplementationKind.ToString()),
                        ("lifetime", registration.Lifetime)),
                });

                AddEdgeIfMissing(new GraphEdge
                {
                    SourceId = registration.RegistrationId,
                    TargetId = registration.ImplementationSymbolId,
                    Kind = EdgeKind.InjectedByDi,
                    Label = registration.Lifetime,
                    Confidence = ToConfidenceScore(registration.Confidence),
                    Properties = CreateProperties(
                        ("projectName", registration.ProjectName),
                        ("namespaceName", registration.NamespaceName),
                        ("filePath", registration.FilePath),
                        ("lineNumber", registration.LineNumber),
                        ("registrationText", registration.RegistrationText),
                        ("lifetime", registration.Lifetime),
                        ("registrationKind", registration.RegistrationKind),
                        ("implementationDisplayName", registration.ImplementationDisplayName)),
                });

                AddEdgeIfMissing(new GraphEdge
                {
                    SourceId = registration.ServiceSymbolId,
                    TargetId = registration.ImplementationSymbolId,
                    Kind = EdgeKind.DiResolvedCall,
                    Label = registration.Lifetime,
                    Confidence = ToConfidenceScore(registration.Confidence),
                    Properties = CreateProperties(
                        ("projectName", registration.ProjectName),
                        ("namespaceName", registration.NamespaceName),
                        ("filePath", registration.FilePath),
                        ("lineNumber", registration.LineNumber),
                        ("registrationText", registration.RegistrationText),
                        ("lifetime", registration.Lifetime),
                        ("registrationKind", registration.RegistrationKind),
                        ("serviceDisplayName", registration.ServiceDisplayName),
                        ("implementationDisplayName", registration.ImplementationDisplayName)),
                });
            }
        }

        void AddOutgoingCallNodesAndEdges(
            ResolvedSymbol sourceSymbol,
            IReadOnlyList<OutgoingCallInfo> outgoingCalls)
        {
            foreach (var outgoingCall in outgoingCalls)
            {
                if (!string.IsNullOrWhiteSpace(outgoingCall.Limitation))
                {
                    var diagnosticKey = $"{outgoingCall.SymbolKey}|{outgoingCall.Limitation}";
                    if (binaryReferenceDiagnostics.Add(diagnosticKey))
                    {
                        diagnostics.Add(new AnalysisDiagnostic
                        {
                            Code = outgoingCall.SymbolOrigin,
                            Message = $"Source could not be resolved for binary-referenced symbol '{outgoingCall.DisplayName}' in assembly '{outgoingCall.AssemblyIdentity}'. Limitation: {outgoingCall.Limitation}.",
                            Confidence = AnalysisConfidence.High,
                        });
                    }
                }

                if (outgoingCall.ExcludedFromGraph)
                {
                    continue;
                }

                AddNodeIfMissing(new GraphNode
                {
                    Id = outgoingCall.SymbolKey,
                    DisplayName = outgoingCall.DisplayName,
                    Kind = outgoingCall.TargetKind,
                    ProjectName = outgoingCall.ProjectName,
                    FilePath = outgoingCall.FilePath,
                    LineNumber = outgoingCall.LineNumber,
                    SymbolKey = outgoingCall.SymbolKey,
                    Properties = CreateProperties(
                        ("projectName", outgoingCall.ProjectName),
                        ("namespaceName", outgoingCall.NamespaceName),
                        ("accessibility", outgoingCall.Accessibility),
                        ("filePath", outgoingCall.FilePath),
                        ("lineNumber", outgoingCall.LineNumber),
                        ("nodeKind", outgoingCall.TargetKind.ToString()),
                        ("symbolOrigin", outgoingCall.SymbolOrigin),
                        ("normalizedFromMetadata", outgoingCall.NormalizedFromMetadata),
                        ("normalizationStrategy", outgoingCall.NormalizationStrategy),
                        ("assemblyIdentity", outgoingCall.AssemblyIdentity),
                        ("limitation", outgoingCall.Limitation)),
                });

                AddEdgeIfMissing(new GraphEdge
                {
                    SourceId = sourceSymbol.SymbolKey,
                    TargetId = outgoingCall.SymbolKey,
                    Kind = outgoingCall.Kind,
                    Label = outgoingCall.Kind.ToString(),
                    Confidence = outgoingCall.Kind switch
                    {
                        EdgeKind.InterfaceDispatch => 0.9d,
                        EdgeKind.UnknownDynamicDispatch => 0.5d,
                        _ => 1.0d,
                    },
                    Properties = CreateProperties(
                        ("projectName", outgoingCall.ProjectName),
                        ("namespaceName", outgoingCall.NamespaceName),
                        ("accessibility", outgoingCall.Accessibility),
                        ("filePath", outgoingCall.FilePath),
                        ("lineNumber", outgoingCall.LineNumber),
                        ("referenceText", outgoingCall.ReferenceText),
                        ("targetSymbol", outgoingCall.DisplayName),
                        ("symbolOrigin", outgoingCall.SymbolOrigin),
                        ("normalizedFromMetadata", outgoingCall.NormalizedFromMetadata),
                        ("normalizationStrategy", outgoingCall.NormalizationStrategy),
                        ("assemblyIdentity", outgoingCall.AssemblyIdentity),
                        ("limitation", outgoingCall.Limitation)),
                });
            }
        }

        static string ResolveWorkspaceLoaderLabel(string? preferredLoader)
        {
            if (!string.IsNullOrWhiteSpace(preferredLoader))
            {
                return preferredLoader;
            }

            return PlatformSupport.IsWindows() ? "msbuild(default)" : "adhoc(default)";
        }

        static IReadOnlyDictionary<string, string> CreateProperties(params (string Key, object? Value)[] items)
        {
            return items
                .Where(static item => item.Value is not null)
                .ToDictionary(
                    static item => item.Key,
                    static item => item.Value switch
                    {
                        bool booleanValue => booleanValue ? "true" : "false",
                        _ => item.Value?.ToString() ?? string.Empty,
                    },
                    StringComparer.Ordinal);
        }

        void ReportProgress(
            AnalysisProgressStage stage,
            string message,
            string? symbolName = null,
            int? depth = null,
            int? expandedSymbolsCount = null)
        {
            request.Progress?.Report(new AnalysisProgressUpdate
            {
                Stage = stage,
                Message = message,
                SymbolName = symbolName ?? request.SymbolName,
                Depth = depth,
                ExpandedSymbols = expandedSymbolsCount,
            });
        }

        AnalysisResult AppendCacheHitDiagnostic(AnalysisResult cached)
        {
            return new AnalysisResult
            {
                Graph = cached.Graph,
                SymbolResolution = cached.SymbolResolution,
                Diagnostics = cached.Diagnostics
                    .Concat(
                    [
                        new AnalysisDiagnostic
                        {
                            Code = "analysis_cache_hit",
                            Message = "Result was served from the in-memory analysis cache.",
                            Confidence = AnalysisConfidence.High,
                        },
                    ])
                    .ToArray(),
            };
        }
    }

    private async Task<IReadOnlyList<SymbolResolutionMatch>> ResolveExpansionSymbolsAsync(
        ISymbol currentSymbol,
        ResolvedSymbol currentResolvedSymbol,
        IReadOnlyList<ReferenceInfo> references,
        IReadOnlyList<ImplementationInfo> implementations,
        IReadOnlyList<EventUsageInfo> eventUsages,
        IReadOnlyList<OutgoingCallInfo> outgoingCalls,
        LoadedSolution loadedSolution,
        List<AnalysisDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, SymbolResolutionMatch>(StringComparer.Ordinal);

        foreach (var reference in references)
        {
            await TryAddResolvedSymbolAsync(reference.ContainingSymbol);
        }

        foreach (var implementation in implementations)
        {
            await TryAddResolvedSymbolAsync(implementation.SymbolKey);
            await TryAddResolvedSymbolAsync(implementation.DisplayName);
        }

        foreach (var eventUsage in eventUsages)
        {
            await TryAddResolvedSymbolAsync(eventUsage.ContainingSymbolId);
            await TryAddResolvedSymbolAsync(eventUsage.ContainingSymbolDisplayName);

            if (!string.IsNullOrWhiteSpace(eventUsage.HandlerSymbolId))
            {
                await TryAddResolvedSymbolAsync(eventUsage.HandlerSymbolId);
            }

            if (!string.IsNullOrWhiteSpace(eventUsage.HandlerName))
            {
                await TryAddResolvedSymbolAsync(eventUsage.HandlerName);
            }
        }

        foreach (var outgoingCall in outgoingCalls)
        {
            await TryAddResolvedSymbolAsync(outgoingCall.SymbolKey);
            await TryAddResolvedSymbolAsync(outgoingCall.DisplayName);
        }

        resolved.Remove(CreateTraversalKey(currentSymbol));
        resolved.Remove(currentResolvedSymbol.SymbolKey);

        return resolved.Values.ToList();

        async Task TryAddResolvedSymbolAsync(string symbolName)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
            {
                return;
            }

            if (resolved.Count >= MaxExpansionCandidatesPerSymbol)
            {
                if (!diagnostics.Any(static diagnostic => diagnostic.Code == "depth_expansion_candidate_limit_reached"))
                {
                    diagnostics.Add(new AnalysisDiagnostic
                    {
                        Code = "depth_expansion_candidate_limit_reached",
                        Message = $"Expansion candidates were capped at {MaxExpansionCandidatesPerSymbol} for a single symbol.",
                        Confidence = AnalysisConfidence.High,
                    });
                }

                return;
            }

            var resolution = await _symbolResolver.ResolveAsync(loadedSolution, symbolName, null, cancellationToken);
            if (resolution is null || resolution.Resolution.Status != SymbolResolutionStatus.Resolved)
            {
                return;
            }

            resolved[CreateTraversalKey(resolution.RoslynSymbol)] = resolution;
        }
    }

    private static string CreateTraversalKey(ISymbol symbol)
    {
        return symbol.GetDocumentationCommentId()
            ?? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static double ToConfidenceScore(AnalysisConfidence confidence)
    {
        return confidence switch
        {
            AnalysisConfidence.Confirmed => 1.0d,
            AnalysisConfidence.High => 0.9d,
            AnalysisConfidence.Estimated => 0.6d,
            AnalysisConfidence.Unclear => 0.3d,
            _ => 0.2d,
        };
    }
}
}
