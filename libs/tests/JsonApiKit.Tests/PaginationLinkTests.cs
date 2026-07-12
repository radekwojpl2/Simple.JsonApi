namespace JsonApiKit.Tests;

public class PaginationLinkTests
{
    [Fact]
    public void Middle_page_has_all_links_and_preserves_other_parameters()
    {
        var query = TestRequests.Parse("?include=company&page[number]=2&page[size]=2",
            new JsonApiQueryOptions { AllowedIncludes = ["company"] });

        var links = query.PageLinks(total: 7);

        Assert.Equal("/api/widgets?include=company&page%5Bnumber%5D=2&page%5Bsize%5D=2", links.Self);
        Assert.Equal("/api/widgets?include=company&page%5Bnumber%5D=1&page%5Bsize%5D=2", links.First);
        Assert.Equal("/api/widgets?include=company&page%5Bnumber%5D=1&page%5Bsize%5D=2", links.Prev);
        Assert.Equal("/api/widgets?include=company&page%5Bnumber%5D=3&page%5Bsize%5D=2", links.Next);
        Assert.Equal("/api/widgets?include=company&page%5Bnumber%5D=4&page%5Bsize%5D=2", links.Last);
    }

    [Fact]
    public void Single_page_omits_prev_and_next()
    {
        var query = TestRequests.Parse("");

        var links = query.PageLinks(total: 3);

        Assert.Null(links.Prev);
        Assert.Null(links.Next);
        Assert.Equal(links.First, links.Last);
    }

    [Fact]
    public void Empty_collection_still_has_one_page()
    {
        var query = TestRequests.Parse("");

        Assert.Equal(new JsonApiMeta(0, 1), query.PageMeta(0));
        Assert.Equal(query.PageLinks(0).First, query.PageLinks(0).Last);
    }

    [Fact]
    public void Page_beyond_last_clamps_prev_to_last_page()
    {
        var query = TestRequests.Parse("?page[number]=99&page[size]=25");

        var links = query.PageLinks(total: 3);

        Assert.Equal("/api/widgets?page%5Bnumber%5D=1&page%5Bsize%5D=25", links.Prev);
        Assert.Null(links.Next);
    }

    [Fact]
    public void Meta_reports_total_and_page_count()
    {
        var query = TestRequests.Parse("?page[size]=2");

        Assert.Equal(new JsonApiMeta(7, 4), query.PageMeta(7));
    }
}
