namespace JsonApiLite.Tests;

/// <summary>Serialize and read back — the document as the other side of the wire sees it.</summary>
internal static class Wire
{
    public static TDocument Roundtrip<TDocument>(TDocument document) =>
        JsonApiSerializer.Deserialize<TDocument>(JsonApiSerializer.Serialize(document))!;
}

public sealed record ContactAttributes(string? FirstName = null, string? LastName = null) : IResourceType
{
    public static string ResourceType => "contacts";
}

public sealed record CompanyAttributes(string? Name = null) : IResourceType
{
    public static string ResourceType => "companies";
}

public sealed record CompanyRelationships
{
    public ToOneRelationship? Owner { get; init; }
}

public sealed record UserAttributes(string? Email = null) : IResourceType
{
    public static string ResourceType => "users";
}

public sealed record TagAttributes(string? Label = null) : IResourceType
{
    public static string ResourceType => "tags";
}

public sealed record ContactRelationships
{
    public ToOneRelationship? Company { get; init; }
    public ToOneRelationship? Manager { get; init; }
    public ToManyRelationship? Tags { get; init; }
}

/// <summary>An endpoint's own meta shapes — the spec reserves no meta member names, so these are
/// what typing meta looks like from the caller's side.</summary>
public sealed record PageMeta(int? Total = null, int? PageCount = null, string? GeneratedAt = null);

public sealed record RoleMeta(string Role);

public sealed record CountMeta(int Count);

public sealed record AttemptMeta(int Attempt);

/// <summary>Meta naming the position it was written in, for pinning that the positions do not
/// bleed into each other.</summary>
public sealed record OriginMeta(string BelongsTo);

public sealed record DealAttributes(string? Title = null, decimal? Amount = null,
    string? Stage = null, DateOnly? CloseDate = null) : IResourceType
{
    public static string ResourceType => "deals";
}

public sealed record DealRelationships
{
    public ToOneRelationship? Company { get; init; }
    public ToOneRelationship? Owner { get; init; }
    public ToManyRelationship? Contacts { get; init; }
}
