using CodeUsageMap.Core.Compatibility;

namespace CodeUsageMap.Core.Symbols
{

public static class WorkspaceLoaderFactory
{
    public static IWorkspaceLoader CreateDefault()
    {
        return Create(Environment.GetEnvironmentVariable("CODEUSAGEMAP_WORKSPACE_LOADER"));
    }

    public static IWorkspaceLoader Create(string? preferredLoader)
    {
        if (string.Equals(preferredLoader, "msbuild", StringComparison.OrdinalIgnoreCase))
        {
            return new MSBuildWorkspaceLoader();
        }

        if (string.Equals(preferredLoader, "adhoc", StringComparison.OrdinalIgnoreCase))
        {
            return new AdhocWorkspaceLoader();
        }

        return PlatformSupport.IsWindows()
            ? new MSBuildWorkspaceLoader()
            : new AdhocWorkspaceLoader();
    }
}
}
