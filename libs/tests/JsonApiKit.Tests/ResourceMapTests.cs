namespace JsonApiKit.Tests;

public class ResourceMapTests
{
    private static readonly WidgetMap Map = new();

    private static readonly Widget Widget = new()
    {
        Id = 7,
        Name = "Sprocket",
        Note = "fragile",
        OwnerId = 3,
        TagIds = [1, 2]
    };

    [Fact]
    public void Build_emits_all_declared_fields()
    {
        var resource = Map.Build(Widget, state: new Dictionary<string, object?> { ["color"] = "red" });

        Assert.Equal("widgets", resource.Type);
        Assert.Equal("7", resource.Id);
        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Equal(new[] { "name", "note", "customFields" }, attributes.Keys);
        Assert.NotNull(resource.Relationships);
        Assert.Equal(new[] { "owner", "tags" }, resource.Relationships.Keys);
    }

    [Fact]
    public void Null_attribute_values_are_omitted()
    {
        var resource = Map.Build(Widget with { Note = null });

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Equal(new[] { "name" }, attributes.Keys);
    }

    [Fact]
    public void Sparse_fieldset_selects_attributes_and_relationships_by_name()
    {
        var resource = Map.Build(Widget, fields: new HashSet<string> { "name", "owner" });

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Equal(new[] { "name" }, attributes.Keys);
        Assert.NotNull(resource.Relationships);
        Assert.Equal(new[] { "owner" }, resource.Relationships.Keys);
    }

    [Fact]
    public void Empty_fieldset_yields_no_attributes_or_relationships()
    {
        var resource = Map.Build(Widget, fields: new HashSet<string>());

        Assert.Null(resource.Attributes);
        Assert.Null(resource.Relationships);
    }

    [Fact]
    public void Null_to_one_id_omits_the_relationship()
    {
        var resource = Map.Build(Widget with { OwnerId = null });

        Assert.NotNull(resource.Relationships);
        Assert.False(resource.Relationships.ContainsKey("owner"));
    }

    [Fact]
    public void To_one_and_to_many_produce_resource_linkage()
    {
        var resource = Map.Build(Widget);

        var owner = Assert.IsType<ResourceIdentifier>(resource.Relationships!["owner"].Data);
        Assert.Equal(new ResourceIdentifier("users", "3"), owner);

        var tags = Assert.IsType<List<ResourceIdentifier>>(resource.Relationships["tags"].Data);
        Assert.Equal(new[] { new ResourceIdentifier("tags", "1"), new ResourceIdentifier("tags", "2") }, tags);
    }

    [Fact]
    public void Self_link_is_emitted_and_survives_sparse_fieldsets()
    {
        Assert.Equal(new ResourceLinks("/api/widgets/7"), Map.Build(Widget).Links);
        Assert.Equal(new ResourceLinks("/api/widgets/7"), Map.Build(Widget, fields: new HashSet<string>()).Links);
    }

    [Fact]
    public void Relationship_links_are_derived_from_the_self_link()
    {
        var relationships = Map.Build(Widget).Relationships!;

        Assert.Equal(new RelationshipLinks("/api/widgets/7/relationships/owner", "/api/widgets/7/owner"),
            relationships["owner"].Links);
        Assert.Null(relationships["tags"].Links); // declared without links: true
    }

    [Fact]
    public void Map_without_self_link_omits_resource_and_relationship_links()
    {
        var resource = new BareWidgetMap().Build(Widget);

        Assert.Null(resource.Links);
        Assert.Null(resource.Relationships!["owner"].Links); // links: true but nothing to derive from
    }

    private sealed class BareWidgetMap : ResourceMap<Widget>
    {
        public override string ResourceType => "widgets";
        protected override string Id(Widget widget) => widget.Id.ToString();

        public BareWidgetMap()
        {
            Attribute("name", w => w.Name);
            ToOne("owner", "users", w => w.OwnerId, links: true);
        }
    }

    [Fact]
    public void Links_only_to_many_emits_a_related_link_and_no_linkage()
    {
        var relationship = new PartsLinkMap().Build(Widget).Relationships!["parts"];

        Assert.Null(relationship.Data); // links-only: the related ids are never loaded
        Assert.Equal(new RelationshipLinks(Self: null, Related: "/api/widgets/7/parts"), relationship.Links);
    }

    [Fact]
    public void Links_only_to_many_is_omitted_without_a_self_link()
    {
        // Nothing to derive the related link from, and a relationship with neither links nor data
        // is not a valid relationship object.
        var resource = new BarePartsLinkMap().Build(Widget);

        Assert.Null(resource.Relationships);
    }

    [Fact]
    public void Links_only_to_many_is_a_selectable_field()
    {
        var map = new PartsLinkMap();

        Assert.True(map.HasField("parts"));
        Assert.Null(map.Build(Widget, fields: new HashSet<string> { "name" }).Relationships);
    }

    private sealed class PartsLinkMap : ResourceMap<Widget>
    {
        public override string ResourceType => "widgets";
        protected override string Id(Widget widget) => widget.Id.ToString();

        public PartsLinkMap()
        {
            SelfLink(w => $"/api/widgets/{w.Id}");
            Attribute("name", w => w.Name);
            ToManyLinks("parts");
        }
    }

    private sealed class BarePartsLinkMap : ResourceMap<Widget>
    {
        public override string ResourceType => "widgets";
        protected override string Id(Widget widget) => widget.Id.ToString();

        public BarePartsLinkMap() => ToManyLinks("parts");
    }

    [Fact]
    public void Registry_resolves_by_entity_and_resource_type()
    {
        var registry = TestRequests.WidgetRegistry();

        Assert.IsType<WidgetMap>(registry.Get<Widget>());
        Assert.True(registry.TryGet("widgets", out var map));
        Assert.True(map.HasField("name"));
        Assert.True(map.HasField("owner"));
        Assert.False(map.HasField("height"));
    }
}
