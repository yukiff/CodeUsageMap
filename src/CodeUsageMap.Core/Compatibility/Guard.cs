using System;
using System.Runtime.InteropServices;

namespace CodeUsageMap.Core.Compatibility;

internal static class Guard
{
    public static void NotNull<T>(T value, string paramName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    public static void NotNullOrWhiteSpace(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
        }
    }
}

internal static class PlatformSupport
{
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
