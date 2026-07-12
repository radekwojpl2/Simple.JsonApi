namespace JsonApiKit;

/// <summary>Per-endpoint policy for JSON:API query parameters, attached as endpoint metadata
/// via <see cref="JsonApiQueryEndpointExtensions.WithJsonApiQuery{TBuilder}"/> and consumed by
/// <see cref="JsonApiQuery.BindAsync"/>.</summary>
public sealed class JsonApiQueryOptions
{
    public IReadOnlyCollection<string> AllowedIncludes { get; init; } = [];

    public IReadOnlyCollection<string> AllowedSorts { get; init; } = [];

    /// <summary>Declared filter names (filter[name]); a null value set accepts any value.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>?> Filters { get; init; } =
        new Dictionary<string, IReadOnlyCollection<string>?>();

    /// <summary>Resource types this endpoint can return (primary + includable), used to document
    /// fields[{type}] sparse-fieldset parameters in OpenAPI. Parsing accepts any registered type
    /// regardless; this is documentation only.</summary>
    public IReadOnlyCollection<string> FieldsTypes { get; init; } = [];

    /// <summary>Whether the endpoint paginates. When false (single-resource and related-resource
    /// endpoints), page[...] parameters are rejected with 400 instead of silently ignored.</summary>
    public bool Paging { get; init; } = true;

    /// <summary>Falls back to <see cref="JsonApiOptions.DefaultPageSize"/> when null.</summary>
    public int? DefaultPageSize { get; init; }

    /// <summary>Falls back to <see cref="JsonApiOptions.MaxPageSize"/> when null.</summary>
    public int? MaxPageSize { get; init; }

    /// <summary>Reject query parameters the endpoint does not understand with 400, per the spec's
    /// query-parameter rules (https://jsonapi.org/format/#query-parameters). Reserved families
    /// (include, sort, page, fields, filter) are validated regardless of this flag.</summary>
    public bool Strict { get; init; } = true;
}
