namespace JsonApiKit;

/// <summary>Base class describing how an entity serializes to a <see cref="ResourceObject"/>.
/// Derived maps declare named attributes and relationships in their constructor; names double as
/// the spec "fields" used by sparse fieldsets (fields[type]=...).</summary>
public abstract class ResourceMap<T> : IResourceMap where T : class
{
    private readonly List<(string Name, Func<T, object?, object?> Selector)> _attributes = [];
    private readonly List<(string Name, Func<T, Relationship?> Factory)> _relationships = [];
    private Func<T, string>? _selfLink;

    public abstract string ResourceType { get; }

    public Type EntityType => typeof(T);

    protected abstract string Id(T entity);

    protected void Attribute(string name, Func<T, object?> selector) =>
        _attributes.Add((name, (entity, _) => selector(entity)));

    /// <summary>Attribute computed from per-call state passed to Build — for data living outside
    /// the entity, like a custom-fields dictionary loaded separately.</summary>
    protected void Attribute(string name, Func<T, object?, object?> selector) =>
        _attributes.Add((name, selector));

    /// <summary>To-one relationship; a null id omits the relationship from the resource object.
    /// With <paramref name="links"/>, emits self/related relationship links derived from
    /// <see cref="SelfLink"/> — only enable it when the API serves those routes.</summary>
    protected void ToOne(string name, string type, Func<T, object?> idSelector, bool links = false) =>
        _relationships.Add((name, entity => idSelector(entity) is { } id
            ? Relationship.ToOne(type, id.ToString()!, links ? LinksFor(entity, name) : null)
            : null));

    protected void ToMany(string name, string type, Func<T, IEnumerable<object>> idsSelector, bool links = false) =>
        _relationships.Add((name, entity =>
            Relationship.ToMany(type, idsSelector(entity).Select(id => id.ToString()!), links ? LinksFor(entity, name) : null)));

    /// <summary>Links-only to-many: emits a related link (.../{name}) and no linkage data, so a
    /// collection response need not load every related id. Omitted from the resource object when
    /// <see cref="SelfLink"/> is not declared — there is no route to derive the link from, and a
    /// relationship object carrying neither links nor data is invalid.</summary>
    protected void ToManyLinks(string name) =>
        _relationships.Add((name, entity => RelatedLinksFor(entity, name) is { } links
            ? new Relationship { Links = links }
            : null));

    /// <summary>Derives .../relationships/{name} and .../{name} from the declared self link.</summary>
    private RelationshipLinks? LinksFor(T entity, string name)
    {
        if (_selfLink is null)
        {
            return null;
        }
        var self = _selfLink(entity);
        return new RelationshipLinks($"{self}/relationships/{name}", $"{self}/{name}");
    }

    /// <summary>Related-only links, for a relationship whose /relationships/{name} route the API
    /// does not serve.</summary>
    private RelationshipLinks? RelatedLinksFor(T entity, string name)
    {
        if (_selfLink is null)
        {
            return null;
        }
        return new RelationshipLinks(Self: null, Related: $"{_selfLink(entity)}/{name}");
    }

    /// <summary>URL of the resource itself, emitted as links.self on every built resource object.
    /// Only declare it when the API actually serves that URL.</summary>
    protected void SelfLink(Func<T, string> link) => _selfLink = link;

    /// <summary>Builds the resource object. <paramref name="fields"/> (from fields[type]) selects a
    /// subset of attributes and relationships by name; null means all. Null attribute values are
    /// omitted, matching serialization of anonymous objects under WhenWritingNull.</summary>
    public ResourceObject Build(T entity, IReadOnlySet<string>? fields = null, object? state = null)
    {
        Dictionary<string, object?>? attributes = null;
        foreach (var (name, selector) in _attributes)
        {
            if (fields is not null && !fields.Contains(name))
            {
                continue;
            }
            if (selector(entity, state) is { } value)
            {
                (attributes ??= []).Add(name, value);
            }
        }

        Dictionary<string, Relationship>? relationships = null;
        foreach (var (name, factory) in _relationships)
        {
            if (fields is not null && !fields.Contains(name))
            {
                continue;
            }
            if (factory(entity) is { } relationship)
            {
                (relationships ??= []).Add(name, relationship);
            }
        }

        return new ResourceObject(ResourceType, Id(entity))
        {
            Attributes = attributes,
            Relationships = relationships,
            Links = _selfLink is not null ? new ResourceLinks(_selfLink(entity)) : null
        };
    }

    /// <summary>Builds the resource object honoring the request's sparse fieldsets for this type.</summary>
    public ResourceObject Build(T entity, JsonApiQuery query, object? state = null) =>
        Build(entity, query.Fields(ResourceType), state);

    public bool HasField(string name) =>
        _attributes.Any(a => a.Name == name) || _relationships.Any(r => r.Name == name);

    public IReadOnlyCollection<string> Fields =>
        [.. _attributes.Select(a => a.Name), .. _relationships.Select(r => r.Name)];
}
