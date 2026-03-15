namespace CodeUsageMap.Integration.Tests.Samples;

public sealed class SamplePublisher
{
    public event EventHandler? WorkCompleted;

    public void Run()
    {
        WorkCompleted += InternalHandler;
        WorkCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void InternalHandler(object? sender, EventArgs args)
    {
    }
}

public sealed class SampleSubscriber
{
    public void Connect(SamplePublisher publisher)
    {
        publisher.WorkCompleted += HandleCompleted;
        publisher.WorkCompleted -= HandleCompleted;
    }

    private void HandleCompleted(object? sender, EventArgs args)
    {
    }
}
