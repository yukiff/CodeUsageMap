using Binary.SourceLib;

namespace Binary.Consumer;

public sealed class ConsumerEntry
{
    public void Execute()
    {
        var worker = BinaryApi.Create();
        worker.Run();
    }
}
