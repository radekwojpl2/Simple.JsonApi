using System.Linq.Expressions;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.CustomFieldDefinitions;

public sealed record CustomFieldDefinitionsResult(IReadOnlyList<CustomFieldDefinition> Definitions, int Total);

public sealed record GetCustomFieldDefinitionsQuery(string? ResourceType, int PageNumber, int PageSize,
    IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<CustomFieldDefinitionsResult>;

public sealed class GetCustomFieldDefinitionsHandler(AppDbContext db)
    : IRequestHandler<GetCustomFieldDefinitionsQuery, CustomFieldDefinitionsResult>
{
    public async Task<CustomFieldDefinitionsResult> Handle(
        GetCustomFieldDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.CustomFieldDefinitions.AsNoTracking().AsQueryable();
        if (request.ResourceType is not null)
        {
            query = query.Where(d => d.ResourceType == request.ResourceType);
        }

        var total = await query.CountAsync(cancellationToken);
        var definitions = await ApplySort(query, request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new CustomFieldDefinitionsResult(definitions, total);
    }

    /// <summary>Field names arrive pre-validated against the endpoint's sort allowlist; Id keeps paging stable.</summary>
    private static IQueryable<CustomFieldDefinition> ApplySort(IQueryable<CustomFieldDefinition> query,
        IReadOnlyList<(string Field, bool Descending)> sort)
    {
        IOrderedQueryable<CustomFieldDefinition>? ordered = null;
        foreach (var (field, descending) in sort)
        {
            Expression<Func<CustomFieldDefinition, object>> key = field switch
            {
                "key" => d => d.Key,
                "resourceType" => d => d.ResourceType,
                _ => throw new ArgumentOutOfRangeException(nameof(sort), field, "Unsupported sort field.")
            };
            ordered = ordered is null
                ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered is null ? query.OrderBy(d => d.Id) : ordered.ThenBy(d => d.Id);
    }
}
