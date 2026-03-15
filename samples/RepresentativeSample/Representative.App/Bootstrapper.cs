using Representative.Core;

namespace Representative.App;

public static class Bootstrapper
{
    public static ServiceRegistry Configure()
    {
        var services = new ServiceRegistry();
        services.AddRepresentativeWorkflow();
        return services;
    }
}
