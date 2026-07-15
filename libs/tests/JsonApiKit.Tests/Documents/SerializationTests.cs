using System.Text.Json;

namespace JsonApiKit.Tests;

public class SerializationTests
{
    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonApiResults.SerializerOptions);

    [Fact]
    public void Document_omits_null_members()
    {
        var json = Serialize(new JsonApiDocument { Data = new List<ResourceObject>() });

        Assert.Equal("""{"data":[]}""", json);
    }

    [Fact]
    public void Resource_object_serializes_mapped_attributes_and_relationships()
    {
        var resource = new WidgetMap().Build(new Widget { Id = 7, Name = "Sprocket", OwnerId = 3, TagIds = [1] });

        var json = Serialize(resource);

        Assert.Equal(
            """{"type":"widgets","id":"7","attributes":{"name":"Sprocket"},"relationships":{"owner":{"links":{"self":"/api/widgets/7/relationships/owner","related":"/api/widgets/7/owner"},"data":{"type":"users","id":"3"}},"tags":{"data":[{"type":"tags","id":"1"}]}},"links":{"self":"/api/widgets/7"}}""",
            json);
    }

    [Fact]
    public void Pagination_links_omit_null_prev_and_next()
    {
        var json = Serialize(new JsonApiLinks(Self: "/w?p=1", First: "/w?p=1", Last: "/w?p=1"));

        Assert.Equal("""{"self":"/w?p=1","first":"/w?p=1","last":"/w?p=1"}""", json);
    }

    [Fact]
    public void To_one_document_emits_explicit_null_data_for_empty_relationships()
    {
        var json = Serialize(new JsonApiToOneDocument
        {
            Data = null,
            Links = new JsonApiLinks(Self: "/api/deals/5/relationships/contact", Related: "/api/deals/5/contact")
        });

        Assert.Equal(
            """{"data":null,"links":{"self":"/api/deals/5/relationships/contact","related":"/api/deals/5/contact"}}""",
            json);
    }

    [Fact]
    public void Links_only_to_many_serializes_as_a_related_link_alone()
    {
        var json = Serialize(new Relationship
        {
            Links = new RelationshipLinks(Self: null, Related: "/api/companies/1/contacts")
        });

        Assert.Equal("""{"links":{"related":"/api/companies/1/contacts"}}""", json);
    }

    [Fact]
    public void Error_status_is_a_string_on_the_wire()
    {
        var json = Serialize(new JsonApiError
        {
            StatusCode = 400,
            Title = "Invalid sort",
            Detail = "Unsupported sort field: height.",
            Source = new JsonApiErrorSource(Parameter: "sort")
        });

        Assert.Equal(
            """{"status":"400","title":"Invalid sort","detail":"Unsupported sort field: height.","source":{"parameter":"sort"}}""",
            json);
    }

    [Fact]
    public void Meta_serializes_total_and_page_count()
    {
        var json = Serialize(new JsonApiMeta(7, 4));

        Assert.Equal("""{"total":7,"pageCount":4}""", json);
    }
}
