# CodeUsageMap

Mac-first scaffold for a Visual Studio usage map extension.

Documents:

- `docs/visual-studio-extension-spec.md`
- `docs/mac-first-development-guide.md`
- `docs/windows-migration-one-pager.md`
- `docs/additional-candidate-tasks.md`
- `docs/validation-coverage-matrix.md`
- `docs/windows-net48-validation-checklist.md`
- `docs/windows-net48-validation-report-template.md`

Implemented so far:

- `Contracts` graph and analysis models
- `Core` analyzer with symbol resolution, reference collection, implementation discovery, and event usage analysis
- `Cli` analyze command with graph JSON, view-model JSON, and DGML output
- JSON / ViewModel JSON outputs now use a shared document envelope with `metadata` + payload
- DGML output now mirrors the shared envelope on the root `DirectedGraph` attributes
- placeholder `Vsix` directory for Windows-only work

Workspace loading:

- macOS and non-Windows default to `AdhocWorkspace`
- Windows defaults to `MSBuildWorkspace`
- override with `CODEUSAGEMAP_WORKSPACE_LOADER=adhoc|msbuild`
- CLI can also force the loader with `--workspace-loader adhoc|msbuild`
- CLI can disambiguate overloaded or weak symbol names with `--symbol-index <n>`

ToolWindow contract output:

- `--format viewmodel-json` emits a UI-oriented payload for the future VSIX `UsageMapViewModel`
- `--format json` emits `{ metadata, graph }`
- `--format viewmodel-json` emits `{ metadata, viewModel }`
- sections are split into `incomingRelations`, `outgoingRelations`, and `relatedRelations`
- node and edge metadata are flattened into detail items for WPF binding
- the VSIX scaffold includes async analysis coordination, cancel support, stale-analysis cancellation on rerun, and status/error text in the tool window
- the VSIX scaffold exposes `Depth`, `Exclude tests`, `Exclude generated`, and `Refresh` that prefers the current editor symbol and falls back to the last analyzed symbol
- the VSIX scaffold exposes `Search`, `Edge`, `Node`, `Project`, and `Min confidence` filters for relation lists
- the VSIX scaffold also exposes a namespace filter backed by analyzer metadata
- the VSIX scaffold also exposes an accessibility filter backed by analyzer metadata
- the VSIX scaffold also exposes a `Hide external` toggle that suppresses metadata-backed external symbols and relations
- the VSIX scaffold also exposes `Hide System.*` and `Hide NuGet` toggles using a framework/package heuristic for external metadata symbols
- the VSIX scaffold also exposes `Highlight interface`, `Highlight events`, and `Highlight async` toggles that fade non-target relations and graph nodes
- the graph canvas preview now applies density control with importance-based node capping and a selected-node focus mode
- the analyzer now detects `AddSingleton` / `AddScoped` / `AddTransient` registrations, including `typeof(...)` forms, and emits `DiResolvedCall` / `InjectedByDi` edges
- `tools/CodeUsageMap.DiProbe` verifies DI registration analysis for generic and `typeof` registrations on macOS
- `tools/CodeUsageMap.NodeAssessmentProbe` verifies shared impact summary and change-risk scoring on macOS
- `samples/RepresentativeSample` is a reusable cross-project validation solution that contains interface dispatch, DI registration, event flow, override, and test-project references
- `samples/MixedDependencySample` is a reusable multi-project validation solution for fan-in / fan-out dependency checks
- `samples/BinaryReferenceSample` is a reusable same-solution binary-reference validation solution that exercises metadata-to-source normalization on macOS
- `tools/CodeUsageMap.RepresentativeSampleProbe` validates the representative sample solution with the real analyzer on macOS
- `tools/CodeUsageMap.MixedDependencyProbe` validates a denser multi-project dependency sample on macOS
- `tools/CodeUsageMap.BinaryReferenceSampleProbe` validates actual-solution same-solution DLL normalization on macOS
- `tools/CodeUsageMap.EdgeKindProbe` validates the union of analyzer-emitted edge kinds on macOS
- `tools/CodeUsageMap.PresentationConsistencyProbe` validates analyzer-to-presentation consistency for shared UsageMap and graph canvas contracts on macOS
- the selected-node details pane now also shows an impact summary with referencing-project count, implementation count, override count, and test-reference presence
- the selected-node details pane now also shows a change-risk summary derived from public API visibility, incoming references, cross-project usage, implementations, overrides, complexity, and test-reference presence
- the representative sample probe now also fixes coverage for `class root`, `property root`, and event `-=` unsubscription on macOS
- the representative sample probe now also verifies analyzer-emitted `Overrides` edges
- the outgoing call collector now emits `UnknownDynamicDispatch` for unresolved dynamic invocation targets
- event subscription analysis now also emits `EventHandlerTarget` edges from the subscription site to the resolved handler
- the VSIX scaffold also exposes a separate root-search box with source-symbol suggestions and reroot actions
- the VSIX scaffold exposes `CallMap`, `DependencyMap`, `InheritanceMap`, and `EventFlow` display modes that filter relations and graph-canvas edges
- the CLI and view-model contract expose `symbolResolution` candidates when a symbol name is ambiguous
- the VSIX scaffold includes a candidate list and can rerun analysis with a selected symbol candidate index
- analyzer diagnostics now surface `ExcludeTests`, `ExcludeGenerated`, ambiguous symbol resolution, and fallback root emission
- JSON envelope now marks ambiguous or unresolved results with `partialResult: true`
- the VSIX scaffold shows diagnostics inline in the tool window
- the VSIX scaffold includes `Export JSON`, `Export ViewModel`, and `Export DGML` buttons backed by a save dialog
- the current VSIX layout shows `root`, `incoming`, `outgoing`, `related`, and a selected-node details pane
- the current VSIX scaffold also includes a graph canvas preview row that places `root` in the center, `inbound` to the left, and `outbound` to the right
- the graph canvas preview now includes breadcrumbs, a mode-aware legend, and a minimap with a viewport overlay
- graph canvas edges now use bundled curved paths instead of straight lines to reduce overlap in the preview
- graph canvas nodes can now be selected, double-clicked to open source, and explicitly rerooted from the preview
- graph canvas preview now also supports right-click actions, Ctrl+wheel zoom, and middle-drag panning
- graph canvas nodes can now collapse or expand their descendant branches in the preview
- the selected-node details pane now shows overview, stats, highlights, and raw properties for the active canvas node
- the current VSIX scaffold paints root symbol information first and then replaces it with the full graph asynchronously
- the core analyzer can publish collector-level progress updates that the VSIX status line can display
- the VSIX status line includes elapsed milliseconds for response-time measurement
- the core presentation layer includes a shared `UsageMapViewModelFilter` so VSIX filtering does not depend on Visual Studio APIs
- same-solution DLL references are normalized toward source via `MetadataSymbolNormalizer` and `SameSolutionAssemblyMatcher`
- unresolved same-solution binary references are suppressed from the graph and surfaced as `unresolved_binary_reference` diagnostics with `limitation=source_not_resolved_from_binary_reference`
- outgoing call node / edge metadata now includes `symbolOrigin`, `normalizedFromMetadata`, `normalizationStrategy`, `assemblyIdentity`, and `limitation`
- external library metadata calls remain visible as `symbolOrigin=metadata` and are not treated as same-solution normalization failures
- `tools/CodeUsageMap.SerializationProbe` validates the shared JSON envelope, ViewModel JSON contract, and DGML root/link metadata on macOS

Current reference classification examples:

- `new Processor()` -> `InstantiatedBy`
- `_processor.Run()` where `_processor` is an interface -> `InterfaceDispatch`
- `processor.Run()` on a concrete type -> `DirectCall`
- `+=` / `-=` -> `EventSubscription` / `EventUnsubscription`
- `SomeEvent?.Invoke(...)` -> `EventRaise` and `EventDispatchEstimated`

Current filtering:

- `--exclude-tests` excludes projects and files that look like test code
- `--exclude-generated` excludes `obj/` and common generated file suffixes

Current depth behavior:

- `--depth 1` analyzes only the selected root symbol
- `--depth 2+` expands through resolved reference owners, implementations, event-related symbols, and outgoing call targets discovered in the current symbol body
- depth expansion currently caps total expanded symbols at `200` and per-symbol expansion candidates at `64`
- the analyzer includes a process-local in-memory cache keyed by `solution + timestamp + symbol + options`
- repeated analysis in the same process appends `analysis_cache_hit`
- the same traversal contract is used on macOS `adhoc` and Windows `msbuild` loaders
- `tools/CodeUsageMap.MetadataNormalizationProbe` verifies both source normalization success and same-solution unresolved limitation behavior on macOS
- `tools/CodeUsageMap.GraphCanvasProbe` verifies the shared graph canvas layout rules for root, inbound, outbound, and related lanes on macOS
