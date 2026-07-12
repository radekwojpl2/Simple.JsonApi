using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiKit;

/// <summary>Parsed and validated JSON:API query parameters (include, sort, page, fields, filter).
/// Declare it as a minimal-API handler parameter; it binds from the request against the endpoint's
/// <see cref="JsonApiQueryOptions"/> metadata and throws <see cref="JsonApiQueryException"/> (→ 400)
/// on invalid input.</summary>
public sealed class JsonApiQuery
{
    private readonly HttpRequest _request;
    private readonly Dictionary<string, IReadOnlySet<string>> _fields;
    private readonly Dictionary<string, string> _filters;

    public IReadOnlySet<string> Includes { get; }

    public Page Page { get; }

    /// <summary>Sort terms in request order; Descending reflects the spec's '-' prefix.</summary>
    public IReadOnlyList<(string Field, bool Descending)> Sort { get; }

    private JsonApiQuery(HttpRequest request, IReadOnlySet<string> includes, Page page,
        IReadOnlyList<(string, bool)> sort, Dictionary<string, IReadOnlySet<string>> fields,
        Dictionary<string, string> filters)
    {
        _request = request;
        Includes = includes;
        Page = page;
        Sort = sort;
        _fields = fields;
        _filters = filters;
    }

    public bool Has(string includePath) => Includes.Contains(includePath);

    /// <summary>Requested sparse fieldset for a resource type, or null when unconstrained.</summary>
    public IReadOnlySet<string>? Fields(string resourceType) => _fields.GetValueOrDefault(resourceType);

    /// <summary>Value of a declared filter[name], or null when the request does not filter on it.</summary>
    public string? Filter(string name) => _filters.GetValueOrDefault(name);

    public static ValueTask<JsonApiQuery> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var options = context.GetEndpoint()?.Metadata.GetMetadata<JsonApiQueryOptions>() ?? new JsonApiQueryOptions();
        var globalOptions = context.RequestServices.GetService<JsonApiOptions>() ?? new JsonApiOptions();
        var registry = context.RequestServices.GetService<ResourceMapRegistry>();
        return ValueTask.FromResult(Parse(context.Request, options, globalOptions, registry));
    }

    public static JsonApiQuery Parse(HttpRequest request, JsonApiQueryOptions options,
        JsonApiOptions globalOptions, ResourceMapRegistry? registry = null)
    {
        var includes = new HashSet<string>();
        var sort = new List<(string Field, bool Descending)>();
        var fields = new Dictionary<string, IReadOnlySet<string>>();
        var filters = new Dictionary<string, string>();
        var pageNumber = 1;
        var pageSize = options.DefaultPageSize ?? globalOptions.DefaultPageSize;
        var maxPageSize = options.MaxPageSize ?? globalOptions.MaxPageSize;

        foreach (var (key, values) in request.Query)
        {
            var value = values.LastOrDefault() ?? "";
            switch (key)
            {
                case "include":
                    foreach (var path in Split(value))
                    {
                        if (!options.AllowedIncludes.Contains(path))
                        {
                            throw Invalid("Invalid include", key,
                                $"Unsupported include path '{path}'. Supported paths: {Supported(options.AllowedIncludes)}.");
                        }
                        includes.Add(path);
                    }
                    break;

                case "sort":
                    foreach (var term in Split(value))
                    {
                        var descending = term.StartsWith('-');
                        var field = descending ? term[1..] : term;
                        if (!options.AllowedSorts.Contains(field))
                        {
                            throw Invalid("Invalid sort", key,
                                $"Unsupported sort field '{field}'. Supported fields: {Supported(options.AllowedSorts)}.");
                        }
                        sort.Add((field, descending));
                    }
                    break;

                case "page[number]" when options.Paging:
                    if (!int.TryParse(value, out pageNumber) || pageNumber < 1)
                    {
                        throw Invalid("Invalid page parameter", key,
                            $"'page[number]' must be a positive integer, got '{value}'.");
                    }
                    break;

                case "page[size]" when options.Paging:
                    if (!int.TryParse(value, out pageSize) || pageSize < 1 || pageSize > maxPageSize)
                    {
                        throw Invalid("Invalid page parameter", key,
                            $"'page[size]' must be an integer between 1 and {maxPageSize}, got '{value}'.");
                    }
                    break;

                // Reserved parameter families the endpoint does not process MUST get 400
                // (https://jsonapi.org/format/#query-parameters), regardless of Strict.
                case "page":
                case var _ when key.StartsWith("page[", StringComparison.Ordinal):
                    throw Invalid("Invalid page parameter", key, options.Paging
                        ? $"Unsupported page parameter '{key}'. Supported parameters: page[number], page[size]."
                        : "This endpoint is not paginated; page parameters are not supported.");

                case var _ when key.StartsWith("fields[", StringComparison.Ordinal) && key.EndsWith(']'):
                    var resourceType = key["fields[".Length..^1];
                    fields[resourceType] = ParseFieldset(resourceType, value, key, registry);
                    break;

                case var _ when key.StartsWith("filter[", StringComparison.Ordinal) && key.EndsWith(']'):
                    var name = key["filter[".Length..^1];
                    if (!options.Filters.TryGetValue(name, out var allowedValues))
                    {
                        throw Invalid("Invalid filter", key,
                            $"Unsupported filter '{name}'. Supported filters: {Supported(options.Filters.Keys)}.");
                    }
                    if (allowedValues is not null && !allowedValues.Contains(value))
                    {
                        throw Invalid("Invalid filter", key,
                            $"Unknown {name} '{value}'. Valid values: {string.Join(", ", allowedValues)}.");
                    }
                    filters[name] = value;
                    break;

                case "fields" or "filter":
                    throw Invalid("Invalid query parameter", key,
                        $"'{key}' must target a member, e.g. {key}[...]=value.");

                default:
                    if (options.Strict)
                    {
                        throw Invalid("Invalid query parameter", key, $"Unknown query parameter '{key}'.");
                    }
                    break;
            }
        }

        return new JsonApiQuery(request, includes, new Page(pageNumber, pageSize), sort, fields, filters);
    }

    private static IReadOnlySet<string> ParseFieldset(string resourceType, string value, string key,
        ResourceMapRegistry? registry)
    {
        IResourceMap? map = null;
        if (registry is not null && !registry.TryGet(resourceType, out map))
        {
            throw Invalid("Invalid fields", key, $"Unknown resource type '{resourceType}'.");
        }

        var fieldset = new HashSet<string>();
        foreach (var field in Split(value))
        {
            if (map is not null && !map.HasField(field))
            {
                throw Invalid("Invalid fields", key, $"Resource type '{resourceType}' has no field '{field}'.");
            }
            fieldset.Add(field);
        }
        return fieldset;
    }

    /// <summary>Top-level pagination links for this request; other query parameters are preserved.</summary>
    public JsonApiLinks PageLinks(int total)
    {
        var pageCount = PageCount(total);
        return new JsonApiLinks(
            Self: Url(Page.Number),
            First: Url(1),
            Prev: Page.Number > 1 ? Url(Math.Min(Page.Number - 1, pageCount)) : null,
            Next: Page.Number < pageCount ? Url(Page.Number + 1) : null,
            Last: Url(pageCount));
    }

    public JsonApiMeta PageMeta(int total) => new(total, PageCount(total));

    /// <summary>An empty collection still has one (empty) page, so first/last links always exist.</summary>
    private int PageCount(int total) => Math.Max(1, (int)Math.Ceiling(total / (double)Page.Size));

    private string Url(int pageNumber)
    {
        var query = new QueryBuilder();
        foreach (var (key, values) in _request.Query)
        {
            if (key is "page[number]" or "page[size]")
            {
                continue;
            }
            foreach (var value in values)
            {
                query.Add(key, value ?? "");
            }
        }

        query.Add("page[number]", pageNumber.ToString());
        query.Add("page[size]", Page.Size.ToString());
        return _request.Path + query.ToQueryString().Value;
    }

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Supported(IEnumerable<string> allowed)
    {
        var list = string.Join(", ", allowed);
        return list.Length > 0 ? list : "(none)";
    }

    private static JsonApiQueryException Invalid(string title, string parameter, string detail) =>
        new(new JsonApiError
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail,
            Source = new JsonApiErrorSource(Parameter: parameter)
        });
}
