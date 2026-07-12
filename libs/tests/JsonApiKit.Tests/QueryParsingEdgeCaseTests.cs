namespace JsonApiKit.Tests;

public class QueryParsingEdgeCaseTests
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
    public void Duplicate_parameter_uses_the_last_value()
    {
        var query = TestRequests.Parse("?sort=name&sort=-amount", Options);

        Assert.Equal(new[] { ("amount", true) }, query.Sort);
    }

    [Fact]
    public void Include_trims_whitespace_and_ignores_empty_entries()
    {
        var query = TestRequests.Parse("?include= company ,,", Options);

        Assert.Equal(new HashSet<string> { "company" }, query.Includes);
    }

    [Fact]
    public void Repeated_include_path_is_deduplicated()
    {
        var query = TestRequests.Parse("?include=company,company", Options);

        Assert.Single(query.Includes);
    }

    [Fact]
    public void Empty_include_value_is_a_noop()
    {
        var query = TestRequests.Parse("?include=");

        Assert.Empty(query.Includes);
    }

    [Fact]
    public void Include_error_lists_the_supported_paths()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?include=invoices", Options));

        Assert.Contains("company", ex.Error.Detail);
    }

    [Fact]
    public void Include_error_reports_none_when_nothing_is_allowed()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?include=x"));

        Assert.Contains("(none)", ex.Error.Detail);
    }

    [Fact]
    public void Filter_error_lists_the_valid_values()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?filter[stage]=bogus", Options));

        Assert.Contains("lead, won", ex.Error.Detail);
    }

    [Theory]
    [InlineData("?fields=name")]
    [InlineData("?filter=x")]
    public void Bare_fields_and_filter_parameters_are_rejected(string queryString)
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse(queryString, Options));

        Assert.Equal(400, ex.Error.StatusCode);
        Assert.Contains("must target a member", ex.Error.Detail);
    }

    [Theory]
    [InlineData("?include=x")]
    [InlineData("?sort=x")]
    [InlineData("?filter[x]=1")]
    [InlineData("?page[foo]=1")]
    public void Lenient_mode_still_validates_reserved_parameter_families(string queryString)
    {
        var options = new JsonApiQueryOptions { Strict = false };

        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse(queryString, options));
    }

    [Fact]
    public void Global_max_page_size_defaults_to_100()
    {
        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?page[size]=101"));
    }

    [Fact]
    public void Sort_with_only_a_dash_is_rejected()
    {
        Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?sort=-", Options));
    }

    [Fact]
    public void Fields_without_a_registry_accepts_any_type_and_field()
    {
        var query = TestRequests.Parse("?fields[gadgets]=whatever", Options);

        Assert.Equal(new HashSet<string> { "whatever" }, query.Fields("gadgets"));
    }

    [Fact]
    public void Page_error_names_the_offending_parameter()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?page[number]=abc", Options));

        Assert.Equal("page[number]", ex.Error.Source?.Parameter);
        Assert.Contains("abc", ex.Error.Detail);
    }

    [Fact]
    public void Query_exception_message_carries_the_error_detail()
    {
        var ex = Assert.Throws<JsonApiQueryException>(() => TestRequests.Parse("?sort=height", Options));

        Assert.Equal(ex.Error.Detail, ex.Message);
    }

    [Fact]
    public void Has_returns_false_for_paths_not_requested()
    {
        var query = TestRequests.Parse("?include=company", Options);

        Assert.False(query.Has("invoices"));
    }
}
