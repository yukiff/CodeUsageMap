using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CodeUsageMap.Vsix.Commands;
using Microsoft.VisualStudio.Shell;

namespace CodeUsageMap.Vsix
{

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("CodeUsageMap", "Usage map visualization for C# solutions", "0.1")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(ToolWindows.UsageMapToolWindow))]
[Guid(PackageGuidString)]
public sealed class CodeUsageMapPackage : AsyncPackage
{
    public const string PackageGuidString = "7A65875A-0D9A-4D74-97F1-E3225D6387D7";

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await ShowUsageMapCommand.InitializeAsync(this);
    }
}
}
