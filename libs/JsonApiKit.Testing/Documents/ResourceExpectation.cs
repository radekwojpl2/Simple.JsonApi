using System.Text.Json.Nodes;

namespace JsonApiKit.Testing;

/// <summary>One expected resource object: type/id, attributes and relationships in declaration
/// order, and links.self. Null attribute values and null to-one ids are omitted, mirroring
/// JsonApiKit's WhenWritingNull serialization and ResourceMap skip rules. A test suite typically
/// wraps this in named per-resource builders that take the entities the test arranged, so ids and
/// values come from what the test planted.</summary>
public sealed class ResourceExpectation(string type, string id, string? selfLink)
{
    private readonly List<(string Name, object? Value)> _attributes = [];
    private readonly List<(string Name, JsonObject Relationship)> _relationships = [];

    /// <summary>Adds an attribute; a null value is omitted from the built resource.</summary>
    public ResourceExpectation Attr(string name, object? value)
    {
        _attributes.Add((name, value));
        return this;
    }

    /// <summary>To-one with self/related links and linkage data; skipped when the id is null.</summary>
    public ResourceExpectation ToOneRel(string name, string targetType, string? targetId)
    {
        if (targetId is { } linkedId)
        {
            _relationships.Add((name, new JsonObject
            {
                ["links"] = new JsonObject
                {
                    ["self"] = $"{selfLink}/relationships/{name}",
                    ["related"] = $"{selfLink}/{name}"
                },
                ["data"] = new JsonObject { ["type"] = targetType, ["id"] = linkedId }
            }));
        }
        return this;
    }

    /// <summary>Links-only to-many: a related link, no linkage data.</summary>
    public ResourceExpectation RelatedOnlyRel(string name)
    {
        _relationships.Add((name, new JsonObject
        {
            ["links"] = new JsonObject { ["related"] = $"{selfLink}/{name}" }
        }));
        return this;
    }

    /// <summary>Sparse fieldset: keep only the named attributes and relationships; links.self
    /// survives, mirroring ResourceMap.Build.</summary>
    public ResourceExpectation Fields(params string[] fields)
    {
        _attributes.RemoveAll(attribute => !fields.Contains(attribute.Name));
        _relationships.RemoveAll(relationship => !fields.Contains(relationship.Name));
        return this;
    }

    internal JsonNode Build()
    {
        var resource = new JsonObject { ["type"] = type, ["id"] = id };

        var attributes = new JsonObject();
        foreach (var (name, value) in _attributes)
        {
            if (value is not null)
            {
                attributes[name] = JsonApiDocuments.ToNode(value);
            }
        }
        if (attributes.Count > 0)
        {
            resource["attributes"] = attributes;
        }

        if (_relationships.Count > 0)
        {
            var relationships = new JsonObject();
            foreach (var (name, relationship) in _relationships)
            {
                relationships[name] = relationship.DeepClone();
            }
            resource["relationships"] = relationships;
        }

        if (selfLink is not null)
        {
            resource["links"] = new JsonObject { ["self"] = selfLink };
        }
        return resource;
    }
}
