using JsonApiLite;

namespace JsonApiPoc.Api;

/// <summary>Attributes as they go out, and as they arrive on a create.</summary>
public sealed record ContactAttributes(
    string? FirstName = null, string? LastName = null, string? Email = null) : IResourceType
{
    public static string ResourceType => "contacts";
}

/// <summary>Attributes as they arrive on a PATCH. Optional&lt;T&gt; keeps "absent" and "explicitly
/// null" apart, which the spec needs: a missing member means keep the current value, an explicit
/// null means clear it.</summary>
public sealed record ContactPatchAttributes(
    Optional<string> FirstName, Optional<string> LastName, Optional<string> Email) : IResourceType
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

public sealed record CompanyRelationships : IRelationships
{
    public ToManyRelationship? Contacts { get; init; }
}

public sealed record TagAttributes(string? Label = null) : IResourceType
{
    public static string ResourceType => "tags";
}

public sealed record TagRelationships : IRelationships;

/// <summary>The spec reserves no meta member names, so a list endpoint declares its own.</summary>
public sealed record PageMeta(int Total, int PageCount) : IMeta;
