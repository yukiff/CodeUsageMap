namespace Representative.Core;

public static class ServiceRegistration
{
    public static ServiceRegistry AddRepresentativeWorkflow(this ServiceRegistry services)
    {
        services.AddScoped<IWorkflow, DefaultWorkflow>();
        services.AddTransient(typeof(IWorkflow), typeof(DefaultWorkflow));
        return services;
    }
}
