using CodeUsageMap.Cli.Formatting;
using CodeUsageMap.Cli.Options;
using CodeUsageMap.Contracts.Analysis;
using CodeUsageMap.Core;
using CodeUsageMap.Core.Presentation;
using CodeUsageMap.Core.Serialization;

namespace CodeUsageMap.Cli.Commands;

public sealed class CliAnalyzeCommand
{
    private readonly CSharpUsageAnalyzer _analyzer;
    private readonly UsageMapViewModelBuilder _viewModelBuilder;
    private readonly UsageGraphJsonSerializer _serializer;
    private readonly ConsoleReporter _reporter;

    public CliAnalyzeCommand()
        : this(new CSharpUsageAnalyzer(), new UsageMapViewModelBuilder(), new UsageGraphJsonSerializer(), new ConsoleReporter())
    {
    }

    public CliAnalyzeCommand(
        CSharpUsageAnalyzer analyzer,
        UsageMapViewModelBuilder viewModelBuilder,
        UsageGraphJsonSerializer serializer,
        ConsoleReporter reporter)
    {
        _analyzer = analyzer;
        _viewModelBuilder = viewModelBuilder;
        _serializer = serializer;
        _reporter = reporter;
    }

    public async Task<int> ExecuteAsync(AnalyzeCommandOptions options, CancellationToken cancellationToken)
    {
        var request = new AnalyzeRequest
        {
            SolutionPath = options.SolutionPath,
            SymbolName = options.SymbolName,
            Options = new AnalyzeOptions
            {
                Depth = options.Depth,
                SymbolIndex = options.SymbolIndex,
                ExcludeGenerated = options.ExcludeGenerated,
                ExcludeTests = options.ExcludeTests,
                WorkspaceLoader = options.WorkspaceLoader,
            },
        };

        var result = await _analyzer.AnalyzeAsync(request, cancellationToken);
        var viewModel = _viewModelBuilder.Build(result);
        var output = options.Format.ToLowerInvariant() switch
        {
            "json" => _serializer.ToJsonDocument(result, request),
            "dgml" => _serializer.ToDgmlDocument(result, request),
            "viewmodel-json" => _serializer.ToViewModelJsonDocument(viewModel, result, request),
            _ => throw new InvalidOperationException($"Unsupported format: {options.Format}"),
        };

        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(options.OutputPath, output, cancellationToken);
        _reporter.WriteSummary(result, options.OutputPath);
        return 0;
    }
}
