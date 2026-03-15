#if NET472
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}

namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.All, Inherited = false)]
internal sealed class SetsRequiredMembersAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.All, Inherited = false)]
internal sealed class MemberNotNullAttribute : Attribute
{
    public MemberNotNullAttribute(params string[] members)
    {
        Members = members;
    }

    public string[] Members { get; }
}

namespace System.Runtime.CompilerServices;

[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Field |
    AttributeTargets.Property |
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute
{
    public CompilerFeatureRequiredAttribute(string featureName)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }

    public bool IsOptional { get; init; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute
{
}
#endif
