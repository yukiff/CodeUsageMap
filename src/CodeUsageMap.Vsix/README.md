# CodeUsageMap.Vsix

Windows-only VSIX scaffold.

Current contents:

- `CodeUsageMap.Vsix.csproj`: Windows build entry point
- `CodeUsageMapPackage`: async package registration
- `ShowUsageMapCommand`: code window context menu command
- `UsageMapToolWindow`: tool window shell
- `UsageMapControl.xaml`: initial WPF layout
- `UsageMapViewModel`: WPF binding layer over `Contracts.Presentation.UsageMapViewModel`
- `VisualStudioSymbolContextService`: active document / caret to symbol resolution
- `NavigationService`: file and line navigation

Notes:

- This project is intentionally not added to `CodeUsageMap.sln` yet, so Mac builds remain green.
- Open `src/CodeUsageMap.Vsix/CodeUsageMap.Vsix.csproj` on Windows with Visual Studio 2022.
- The command currently forces `WorkspaceLoader = "msbuild"` because that is the intended Windows default path.
