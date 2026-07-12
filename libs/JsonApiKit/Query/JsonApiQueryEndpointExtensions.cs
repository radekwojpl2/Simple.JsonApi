using Microsoft.AspNetCore.Builder;

namespace JsonApiKit;

public static class JsonApiQueryEndpointExtensions
{
    public static TBuilder WithJsonApiQuery<TBuilder>(this TBuilder builder,
        string[]? includes = null,
        string[]? sorts = null,
        Dictionary<string, string[]?>? filters = null,
        string[]? fieldsFor = null,
        bool paging = true,
        int? defaultPageSize = null,
        int? maxPageSize = null,
        bool strict = true)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new JsonApiQueryOptions
        {
            AllowedIncludes = includes ?? [],
            AllowedSorts = sorts ?? [],
            Filters = filters?.ToDictionary(f => f.Key, f => (IReadOnlyCollection<string>?)f.Value)
                      ?? new Dictionary<string, IReadOnlyCollection<string>?>(),
            FieldsTypes = fieldsFor ?? [],
            Paging = paging,
            DefaultPageSize = defaultPageSize,
            MaxPageSize = maxPageSize,
            Strict = strict
        });
}
