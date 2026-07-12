namespace JsonApiKit.Tests;

public class ResourceMapBuildTests
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
    public void Build_with_query_applies_the_requested_sparse_fieldset()
    {
        var query = TestRequests.Parse("?fields[widgets]=name", registry: TestRequests.WidgetRegistry());

        var resource = Map.Build(Widget, query);

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Equal(new[] { "name" }, attributes.Keys);
        Assert.Null(resource.Relationships);
    }

    [Fact]
    public void Build_with_query_without_fieldset_emits_everything()
    {
        var query = TestRequests.Parse("");

        var resource = Map.Build(Widget, query);

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Contains("name", attributes.Keys);
        Assert.NotNull(resource.Relationships);
    }

    [Fact]
    public void Build_with_query_only_honors_fieldsets_for_its_own_type()
    {
        var query = TestRequests.Parse("?fields[users]=email", registry: null);

        var resource = Map.Build(Widget, query);

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Contains("name", attributes.Keys);
    }

    [Fact]
    public void Fields_lists_attributes_then_relationships()
    {
        Assert.Equal(["name", "note", "customFields", "owner", "tags"], Map.Fields);
    }

    [Fact]
    public void State_backed_attribute_receives_the_per_call_state()
    {
        var state = new Dictionary<string, object?> { ["color"] = "red" };

        var resource = Map.Build(Widget, state: state);

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.Same(state, attributes["customFields"]);
    }

    [Fact]
    public void Null_state_omits_the_state_backed_attribute()
    {
        var resource = Map.Build(Widget);

        var attributes = Assert.IsType<Dictionary<string, object?>>(resource.Attributes);
        Assert.False(attributes.ContainsKey("customFields"));
    }

    [Fact]
    public void To_many_with_no_ids_still_emits_the_relationship_with_empty_linkage()
    {
        var resource = Map.Build(Widget with { TagIds = [] });

        var tags = Assert.IsType<List<ResourceIdentifier>>(resource.Relationships!["tags"].Data);
        Assert.Empty(tags);
    }

    [Fact]
    public void Entity_type_reports_the_mapped_clr_type()
    {
        Assert.Equal(typeof(Widget), Map.EntityType);
    }
}
