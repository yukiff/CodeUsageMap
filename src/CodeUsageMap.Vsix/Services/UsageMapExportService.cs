using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeUsageMap.Core.Serialization;
using CodeUsageMap.Vsix.ViewModels;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

namespace CodeUsageMap.Vsix.Services;

internal sealed class UsageMapExportService
{
    private readonly UsageGraphJsonSerializer _serializer = new();

    public async Task<string?> ExportAsync(
        UsageMapExportSnapshot snapshot,
        UsageMapExportFormat format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dialog = new SaveFileDialog
        {
            Title = "Export Usage Map",
            Filter = BuildFilter(format),
            DefaultExt = BuildExtension(format),
            FileName = BuildFileName(snapshot, format),
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var content = format switch
        {
            UsageMapExportFormat.Json => _serializer.ToJsonDocument(snapshot.Result, snapshot.Request),
            UsageMapExportFormat.ViewModelJson => _serializer.ToViewModelJsonDocument(snapshot.ViewModel, snapshot.Result, snapshot.Request),
            UsageMapExportFormat.Dgml => _serializer.ToDgmlDocument(snapshot.Result, snapshot.Request),
            _ => throw new InvalidOperationException($"Unsupported export format: {format}"),
        };

        var directory = Path.GetDirectoryName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(dialog.FileName, content, cancellationToken);
        return dialog.FileName;
    }

    private static string BuildFileName(UsageMapExportSnapshot snapshot, UsageMapExportFormat format)
    {
        var symbolName = SanitizeFileName(snapshot.Request.SymbolName);
        var suffix = format switch
        {
            UsageMapExportFormat.Json => ".json",
            UsageMapExportFormat.ViewModelJson => ".viewmodel.json",
            UsageMapExportFormat.Dgml => ".dgml",
            _ => ".txt",
        };

        return $"{symbolName}{suffix}";
    }

    private static string BuildFilter(UsageMapExportFormat format)
    {
        return format switch
        {
            UsageMapExportFormat.Json => "JSON files (*.json)|*.json",
            UsageMapExportFormat.ViewModelJson => "JSON files (*.json)|*.json",
            UsageMapExportFormat.Dgml => "DGML files (*.dgml)|*.dgml",
            _ => "All files (*.*)|*.*",
        };
    }

    private static string BuildExtension(UsageMapExportFormat format)
    {
        return format switch
        {
            UsageMapExportFormat.Json => ".json",
            UsageMapExportFormat.ViewModelJson => ".json",
            UsageMapExportFormat.Dgml => ".dgml",
            _ => ".txt",
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "usage-map" : sanitized;
    }
}
