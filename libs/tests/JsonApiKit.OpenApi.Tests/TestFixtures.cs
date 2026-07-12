using JsonApiKit;

namespace JsonApiKit.OpenApi.Tests;

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
        Attribute("name", w => w.Name);
        Attribute("note", w => w.Note);
        Attribute("customFields", (_, state) => state);
        ToOne("owner", "users", w => w.OwnerId);
        ToMany("tags", "tags", w => w.TagIds.Cast<object>());
    }
}
