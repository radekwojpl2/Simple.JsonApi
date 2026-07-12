using Microsoft.AspNetCore.Http;

namespace JsonApiKit.Tests;

public sealed record Widget
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Note { get; init; }
    public int? OwnerId { get; init; }
    public List<int> TagIds { get; init; } = [];
}

public sealed class WidgetMap : ResourceMap<Widget>
{
    public override string ResourceType => "widgets";
    protected override string Id(Widget widget) => widget.Id.ToString();

    public WidgetMap()
    {
        SelfLink(w => $"/api/widgets/{w.Id}");
        Attribute("name", w => w.Name);
        Attribute("note", w => w.Note);
        Attribute("customFields", (_, state) => state);
        ToOne("owner", "users", w => w.OwnerId, links: true);
        ToMany("tags", "tags", w => w.TagIds.Cast<object>());
    }
}

public static class TestRequests
{
    public static HttpRequest Request(string queryString, string path = "/api/widgets")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        return context.Request;
    }

    public static JsonApiQuery Parse(string queryString, JsonApiQueryOptions? options = null,
        ResourceMapRegistry? registry = null) =>
        JsonApiQuery.Parse(Request(queryString), options ?? new JsonApiQueryOptions(), new JsonApiOptions(), registry);

    public static ResourceMapRegistry WidgetRegistry() => new([new WidgetMap()]);
}
