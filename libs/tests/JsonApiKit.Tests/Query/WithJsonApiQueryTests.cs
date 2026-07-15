using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace JsonApiKit.Tests;

public class WithJsonApiQueryTests
{
    private sealed class CapturingBuilder : IEndpointConventionBuilder
    {
        private readonly List<Action<EndpointBuilder>> _conventions = [];

        public void Add(Action<EndpointBuilder> convention) => _conventions.Add(convention);

        public JsonApiQueryOptions BuiltOptions()
        {
            var builder = new RouteEndpointBuilder(null, RoutePatternFactory.Parse("/"), 0);
            foreach (var convention in _conventions)
            {
                convention(builder);
            }
            return Assert.Single(builder.Metadata.OfType<JsonApiQueryOptions>());
        }
    }

    [Fact]
    public void Defaults_produce_strict_paged_options_with_empty_allowlists()
    {
        var options = new CapturingBuilder().WithJsonApiQuery().BuiltOptions();

        Assert.Empty(options.AllowedIncludes);
        Assert.Empty(options.AllowedSorts);
        Assert.Empty(options.Filters);
        Assert.Empty(options.FieldsTypes);
        Assert.True(options.Paging);
        Assert.True(options.Strict);
        Assert.Null(options.DefaultPageSize);
        Assert.Null(options.MaxPageSize);
    }

    [Fact]
    public void Arguments_map_onto_the_metadata()
    {
        var options = new CapturingBuilder().WithJsonApiQuery(
            includes: ["company"],
            sorts: ["name"],
            filters: new Dictionary<string, string[]?> { ["stage"] = ["lead"], ["search"] = null },
            fieldsFor: ["widgets"],
            paging: false,
            defaultPageSize: 5,
            maxPageSize: 50,
            strict: false).BuiltOptions();

        Assert.Equal(["company"], options.AllowedIncludes);
        Assert.Equal(["name"], options.AllowedSorts);
        Assert.Equal(["lead"], options.Filters["stage"]);
        Assert.Null(options.Filters["search"]);
        Assert.Equal(["widgets"], options.FieldsTypes);
        Assert.False(options.Paging);
        Assert.False(options.Strict);
        Assert.Equal(5, options.DefaultPageSize);
        Assert.Equal(50, options.MaxPageSize);
    }

    [Fact]
    public void Built_options_drive_parsing_like_hand_written_ones()
    {
        var options = new CapturingBuilder()
            .WithJsonApiQuery(includes: ["company"], paging: false)
            .BuiltOptions();

        var query = TestRequests.Parse("?include=company", options);
        Assert.True(query.Has("company"));
        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?page[number]=2", options));
    }
}
