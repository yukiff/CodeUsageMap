using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace CodeUsageMap.Vsix.Services
{

internal sealed class NavigationService
{
    public async Task NavigateAsync(string filePath, int? lineNumber)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
        if (dte is null || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var window = dte.ItemOperations.OpenFile(filePath);
        window.Visible = true;

        if (lineNumber is not null && dte.ActiveDocument?.Selection is TextSelection selection)
        {
            selection.GotoLine(lineNumber.Value, Select: false);
        }
    }
}
}
