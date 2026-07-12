using Microsoft.Extensions.DependencyInjection;

namespace JsonApiKit;

public static class JsonApiServiceCollectionExtensions
{
    /// <summary>Registers JSON:API services: global options, resource maps + registry, and the
    /// query-binding exception handler (picked up by UseExceptionHandler).</summary>
    public static IServiceCollection AddJsonApi(this IServiceCollection services, Action<JsonApiOptions>? configure = null)
    {
        var options = new JsonApiOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        foreach (var mapType in options.MapTypes)
        {
            services.AddSingleton(typeof(IResourceMap), mapType);
        }
        services.AddSingleton<ResourceMapRegistry>();
        services.AddExceptionHandler<JsonApiQueryExceptionHandler>();
        return services;
    }
}
