namespace CodeUsageMap.Integration.Tests.Samples;

[Marker]
public interface IProcessor
{
    void Run();
}

public sealed class Processor : IProcessor
{
    [Marker]
    public void Run()
    {
    }
}

[Marker]
public sealed class ProcessorConsumer
{
    private readonly IProcessor _processor = new Processor();

    public void Execute()
    {
        _processor.Run();

        var processor = new Processor();
        processor.Run();
    }
}

public sealed class ProcessorWorkflow
{
    private readonly ProcessorConsumer _consumer = new();

    public void Start()
    {
        _consumer.Execute();
    }
}

public sealed class MarkerAttribute : Attribute
{
}
