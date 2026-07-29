namespace JsonApiLite.OpenApi.Tests;

// Shapes mirroring JsonApiPoc.Api/Contracts.cs, so the fixtures and the sample exercise the same
// things. Where a fixture has no counterpart in the sample it is because the sample cannot reach
// the case — a self-referencing meta shape, or a sideload shape naming no types at all.

public sealed record ContactAttributes(
    string? FirstName = null, string? LastName = null, string? Email = null) : IResourceType
{
    public static string ResourceType => "contacts";
}

public sealed record ContactRelationships : IRelationships
{
    public ToOneRelationship? Company { get; init; }
    public ToManyRelationship? Tags { get; init; }
}

public sealed record CompanyAttributes(string? Name = null) : IResourceType
{
    public static string ResourceType => "companies";
}

public sealed record CompanyRelationships : IRelationships;

public sealed record TagAttributes(string? Label = null) : IResourceType
{
    public static string ResourceType => "tags";
}

public sealed record TagRelationships : IRelationships;

/// <summary>A declared meta shape: a plain record, so reflecting it describes what is serialized.</summary>
public sealed record PageMeta(int Total, int PageCount) : IMeta;

public enum Ordering
{
    Ascending,
    Descending,
}

/// <summary>Nesting, a list and an enum in one shape, to pin that the meta walker reaches the same
/// depth and uses the same conventions as the attributes walker.</summary>
public sealed record NestedMeta : IMeta
{
    public PageMeta? Page { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public Ordering Order { get; init; }
    public IReadOnlyDictionary<string, int>? Counts { get; init; }
}

/// <summary>Refers to itself, so the walker must terminate rather than recurse forever.</summary>
public sealed record RecursiveMeta : IMeta
{
    public RecursiveMeta? Parent { get; init; }
    public int Depth { get; init; }
}

/// <summary>Two declared sideloadable types, as the sample's ContactIncluded declares.</summary>
public sealed record ContactIncluded : IIncluded
{
    public IReadOnlyList<Resource<CompanyAttributes, CompanyRelationships>>? Companies { get; init; }

    public IReadOnlyList<Resource<TagAttributes, TagRelationships>>? Tags { get; init; }
}

/// <summary>A sideload shape naming a resource type that is also the primary data's type, which the
/// specification permits and the description must not let collide with the primary data's schema.</summary>
public sealed record SelfIncluded : IIncluded
{
    public IReadOnlyList<Resource<ContactAttributes, ContactRelationships>>? Contacts { get; init; }
}

/// <summary>Declares nothing. Must be described exactly as no declaration is — never as a list that
/// can hold nothing.</summary>
public sealed record EmptyIncluded : IIncluded;
