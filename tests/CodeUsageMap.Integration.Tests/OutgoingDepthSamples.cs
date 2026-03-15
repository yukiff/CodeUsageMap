namespace CodeUsageMap.Integration.Tests.OutgoingSamples;

public sealed class PipelineStart
{
    public void Start()
    {
        var step = new PipelineStep();
        step.Run();
    }
}

public sealed class PipelineStep
{
    public void Run()
    {
        PipelineLeaf.Execute();
    }
}

public static class PipelineLeaf
{
    public static void Execute()
    {
    }
}

public sealed class DynamicPipeline
{
    public void Execute()
    {
        dynamic step = new DynamicStep();
        step.Run();
    }
}

public sealed class DynamicStep
{
    public void Run()
    {
    }
}
