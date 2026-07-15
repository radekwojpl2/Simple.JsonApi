namespace JsonApiKit.Tests;

public class DocumentModelTests
{
    [Fact]
    public void To_one_factory_wraps_a_single_identifier()
    {
        var relationship = Relationship.ToOne("users", "3");

        Assert.Equal(new ResourceIdentifier("users", "3"), relationship.Data);
        Assert.Null(relationship.Links);
    }

    [Fact]
    public void To_one_factory_carries_links_through()
    {
        var links = new RelationshipLinks("/w/7/relationships/owner", "/w/7/owner");

        var relationship = Relationship.ToOne("users", "3", links);

        Assert.Same(links, relationship.Links);
    }

    [Fact]
    public void To_many_factory_wraps_identifiers_in_request_order()
    {
        var relationship = Relationship.ToMany("tags", ["2", "1"]);

        Assert.Equal(
            new List<ResourceIdentifier> { new("tags", "2"), new("tags", "1") },
            relationship.Data);
    }

    [Fact]
    public void To_many_factory_yields_an_empty_list_for_no_ids()
    {
        var relationship = Relationship.ToMany("tags", []);

        Assert.Empty(Assert.IsType<List<ResourceIdentifier>>(relationship.Data));
    }

    [Fact]
    public void Registry_get_throws_a_helpful_error_for_unregistered_types()
    {
        var registry = new ResourceMapRegistry([]);

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Get<Widget>());

        Assert.Contains("Widget", ex.Message);
        Assert.Contains("AddJsonApi", ex.Message);
    }

    [Fact]
    public void Registry_try_get_returns_false_for_unknown_resource_types()
    {
        var registry = TestRequests.WidgetRegistry();

        Assert.False(registry.TryGet("gadgets", out var map));
        Assert.Null(map);
    }

    [Fact]
    public void Query_exception_exposes_the_error_and_uses_its_detail_as_message()
    {
        var error = new JsonApiError { StatusCode = 400, Detail = "bad input" };

        var exception = new JsonApiQueryException(error);

        Assert.Same(error, exception.Error);
        Assert.Equal("bad input", exception.Message);
    }

    [Fact]
    public void Error_status_string_tracks_the_numeric_status_code()
    {
        Assert.Equal("404", new JsonApiError { StatusCode = 404 }.Status);
        Assert.Equal("400", new JsonApiError().Status); // default
    }
}
