namespace Binary.SourceLib;

public static class BinaryApi
{
    public static BinaryWorker Create()
    {
        return new BinaryWorker();
    }
}

public sealed class BinaryWorker
{
    public void Run()
    {
    }
}
