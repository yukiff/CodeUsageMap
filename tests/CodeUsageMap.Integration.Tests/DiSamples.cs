using System;
using System.Threading.Tasks;

namespace CodeUsageMap.Integration.Tests.DiSamples;

public interface IServiceCollection
{
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        return services;
    }

    public static IServiceCollection AddTransient(this IServiceCollection services, Type serviceType, Type implementationType)
    {
        return services;
    }
}

public interface IOrderService
{
    Task SubmitAsync();
}

public sealed class OrderService : IOrderService
{
    public Task SubmitAsync()
    {
        return Task.CompletedTask;
    }
}

public interface IReportService
{
    void Publish();
}

public sealed class ReportService : IReportService
{
    public void Publish()
    {
    }
}

public static class DiSetup
{
    public static void Configure(IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddTransient(typeof(IReportService), typeof(ReportService));
    }
}
