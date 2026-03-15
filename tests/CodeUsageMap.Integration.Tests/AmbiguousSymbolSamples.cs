namespace CodeUsageMap.Integration.Tests.AmbiguousSamples;

public sealed class OverloadedProcessor
{
    public void Handle()
    {
    }

    public void Handle(string value)
    {
    }
}
