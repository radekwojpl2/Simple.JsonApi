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
