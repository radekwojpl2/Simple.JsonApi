using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiKit.Tests;

public class AddJsonApiTests
{
    private static ServiceProvider Build(Action<JsonApiOptions>? configure = null) =>
        new ServiceCollection().AddJsonApi(configure).BuildServiceProvider();

    [Fact]
    public void Exposes_configured_options_as_a_singleton()
    {
        using var provider = Build(o =>
        {
            o.DefaultPageSize = 10;
            o.MaxPageSize = 50;
        });

        var options = provider.GetRequiredService<JsonApiOptions>();
        Assert.Equal(10, options.DefaultPageSize);
        Assert.Equal(50, options.MaxPageSize);
        Assert.Same(options, provider.GetRequiredService<JsonApiOptions>());
    }

    [Fact]
    public void Registers_added_maps_and_a_registry_over_them()
    {
        using var provider = Build(o => o.AddMap<WidgetMap>());

        Assert.IsType<WidgetMap>(Assert.Single(provider.GetServices<IResourceMap>()));
        Assert.IsType<WidgetMap>(provider.GetRequiredService<ResourceMapRegistry>().Get<Widget>());
    }

    [Fact]
    public void Registers_the_query_exception_handler()
    {
        using var provider = Build();

        Assert.Contains(provider.GetServices<IExceptionHandler>(),
            handler => handler is JsonApiQueryExceptionHandler);
    }

    [Fact]
    public void Registry_without_maps_resolves_but_knows_nothing()
    {
        using var provider = Build();

        var registry = provider.GetRequiredService<ResourceMapRegistry>();
        Assert.False(registry.TryGet("widgets", out _));
    }
}
