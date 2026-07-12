namespace JsonApiKit.Tests;

public class JsonApiQueryParsingTests
{
    private static readonly JsonApiQueryOptions Options = new()
    {
        AllowedIncludes = ["company"],
        AllowedSorts = ["name", "amount"],
        Filters = new Dictionary<string, IReadOnlyCollection<string>?>
        {
            ["stage"] = ["lead", "won"],
            ["search"] = null
        }
    };

    [Fact]
    public void Empty_query_uses_defaults()
    {
        var query = TestRequests.Parse("", Options);

        Assert.Empty(query.Includes);
        Assert.Empty(query.Sort);
        Assert.Equal(new Page(1, 25), query.Page);
        Assert.Null(query.Fields("widgets"));
        Assert.Null(query.Filter("stage"));
    }

    [Fact]
    public void Include_parses_allowed_paths()
    {
        var query = TestRequests.Parse("?include=company", Options);

        Assert.True(query.Has("company"));
    }

    [Fact]
    public void Include_rejects_unknown_path()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?include=invoices", Options));

        Assert.Equal(400, ex.Error.StatusCode);
        Assert.Equal("include", ex.Error.Source?.Parameter);
    }

    [Fact]
    public void Sort_parses_direction_and_order()
    {
        var query = TestRequests.Parse("?sort=-amount,name", Options);

        Assert.Equal(new[] { ("amount", true), ("name", false) }, query.Sort);
    }

    [Fact]
    public void Sort_rejects_unknown_field()
    {
        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?sort=height", Options));
    }

    [Theory]
    [InlineData("?page[number]=3&page[size]=10", 3, 10)]
    [InlineData("?page[size]=100", 1, 100)]
    public void Page_parses_number_and_size(string queryString, int number, int size)
    {
        var query = TestRequests.Parse(queryString, Options);

        Assert.Equal(new Page(number, size), query.Page);
    }

    [Theory]
    [InlineData("?page[number]=0")]
    [InlineData("?page[number]=abc")]
    [InlineData("?page[size]=0")]
    [InlineData("?page[size]=101")]
    [InlineData("?page[offset]=10")]
    [InlineData("?page[]=1")]
    [InlineData("?page=2")]
    public void Page_rejects_invalid_and_unsupported_parameters(string queryString)
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse(queryString, Options));

        Assert.Equal(400, ex.Error.StatusCode);
    }

    [Fact]
    public void Filter_accepts_declared_value()
    {
        var query = TestRequests.Parse("?filter[stage]=won&filter[search]=acme", Options);

        Assert.Equal("won", query.Filter("stage"));
        Assert.Equal("acme", query.Filter("search"));
    }

    [Fact]
    public void Filter_rejects_value_outside_allowed_set()
    {
        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?filter[stage]=bogus", Options));
    }

    [Fact]
    public void Filter_rejects_undeclared_name()
    {
        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?filter[color]=red", Options));
    }

    [Fact]
    public void Fields_parses_per_type_sets_with_registry_validation()
    {
        var query = TestRequests.Parse("?fields[widgets]=name,owner", Options, TestRequests.WidgetRegistry());

        Assert.Equal(new HashSet<string> { "name", "owner" }, query.Fields("widgets"));
        Assert.Null(query.Fields("users"));
    }

    [Fact]
    public void Fields_rejects_unknown_resource_type()
    {
        Assert.Throws<JsonApiQueryException>(() =>
            TestRequests.Parse("?fields[gadgets]=name", Options, TestRequests.WidgetRegistry()));
    }

    [Fact]
    public void Fields_rejects_unknown_field_name()
    {
        Assert.Throws<JsonApiQueryException>(() =>
            TestRequests.Parse("?fields[widgets]=height", Options, TestRequests.WidgetRegistry()));
    }

    [Fact]
    public void Strict_mode_rejects_unknown_parameters()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?foo=bar", Options));

        Assert.Equal("foo", ex.Error.Source?.Parameter);
    }

    [Fact]
    public void Lenient_mode_ignores_unknown_parameters()
    {
        var options = new JsonApiQueryOptions { Strict = false };

        var query = TestRequests.Parse("?foo=bar", options);

        Assert.Equal(new Page(1, 25), query.Page);
    }

    [Fact]
    public void Unpaged_endpoint_rejects_page_parameters()
    {
        var options = new JsonApiQueryOptions { Paging = false };

        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?page[number]=2", options));

        Assert.Equal(400, ex.Error.StatusCode);
        Assert.Contains("not paginated", ex.Error.Detail);
    }

    [Fact]
    public void Endpoint_page_size_overrides_global_default()
    {
        var options = new JsonApiQueryOptions { DefaultPageSize = 5, MaxPageSize = 10 };

        var query = TestRequests.Parse("", options);
        Assert.Equal(new Page(1, 5), query.Page);

        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?page[size]=11", options));
    }
}
