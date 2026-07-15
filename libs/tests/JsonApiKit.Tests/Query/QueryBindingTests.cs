using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiKit.Tests;

public class QueryBindingTests
{
    private static HttpContext Context(string queryString, JsonApiQueryOptions? endpointOptions = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);

        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        context.RequestServices = services.BuildServiceProvider();

        if (endpointOptions is not null)
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(endpointOptions), "test"));
        }
        return context;
    }

    [Fact]
    public async Task BindAsync_reads_options_from_endpoint_metadata()
    {
        var context = Context("?include=company", new JsonApiQueryOptions { AllowedIncludes = ["company"] });

        var query = await JsonApiQuery.BindAsync(context, null!);

        Assert.True(query.Has("company"));
    }

    [Fact]
    public async Task BindAsync_uses_defaults_without_endpoint_metadata()
    {
        var query = await JsonApiQuery.BindAsync(Context(""), null!);

        Assert.Equal(new Page(1, 25), query.Page);
    }

    [Fact]
    public async Task BindAsync_uses_global_options_from_services()
    {
        var context = Context("",
            configureServices: s => s.AddSingleton(new JsonApiOptions { DefaultPageSize = 5 }));

        var query = await JsonApiQuery.BindAsync(context, null!);

        Assert.Equal(new Page(1, 5), query.Page);
    }

    [Fact]
    public async Task BindAsync_validates_fields_against_the_registered_maps()
    {
        var context = Context("?fields[widgets]=name",
            configureServices: s => s.AddSingleton(TestRequests.WidgetRegistry()));

        var query = await JsonApiQuery.BindAsync(context, null!);
        Assert.Equal(new HashSet<string> { "name" }, query.Fields("widgets"));

        var invalid = Context("?fields[widgets]=height",
            configureServices: s => s.AddSingleton(TestRequests.WidgetRegistry()));
        Assert.Throws<JsonApiQueryException>(() => JsonApiQuery.BindAsync(invalid, null!));
    }

    [Fact]
    public void BindAsync_throws_for_invalid_parameters()
    {
        Assert.Throws<JsonApiQueryException>(() => JsonApiQuery.BindAsync(Context("?include=x"), null!));
    }
}
