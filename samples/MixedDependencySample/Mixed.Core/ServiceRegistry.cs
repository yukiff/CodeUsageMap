namespace Mixed.Core;

public sealed class ServiceRegistry
{
    public ServiceRegistry AddScoped<TService, TImplementation>()
        where TImplementation : TService
    {
        return this;
    }

    public ServiceRegistry AddTransient(Type serviceType, Type implementationType)
    {
        return this;
    }
}
