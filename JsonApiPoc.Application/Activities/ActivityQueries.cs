using System.Linq.Expressions;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Activities;

public sealed record ActivitiesResult(
    IReadOnlyList<Activity> Activities,
    int Total,
    IReadOnlyList<Deal>? Deals,
    IReadOnlyDictionary<int, Dictionary<string, object?>>? DealFields,
    IReadOnlyList<Contact>? Contacts,
    IReadOnlyDictionary<int, Dictionary<string, object?>>? ContactFields);

public sealed record GetActivitiesQuery(bool IncludeDeal, bool IncludeContact, int PageNumber, int PageSize,
    IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<ActivitiesResult>;

public sealed record GetActivityByIdQuery(int Id, bool IncludeDeal, bool IncludeContact) : IRequest<ActivitiesResult?>;

public sealed class GetActivitiesHandler(AppDbContext db) : IRequestHandler<GetActivitiesQuery, ActivitiesResult>
{
    public async Task<ActivitiesResult> Handle(GetActivitiesQuery request, CancellationToken cancellationToken)
    {
        var total = await db.Activities.CountAsync(cancellationToken);
        var activities = await ApplySort(db.Activities.AsNoTracking(), request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return await ActivityIncludes.LoadAsync(db, activities, total, request.IncludeDeal, request.IncludeContact, cancellationToken);
    }

    /// <summary>Field names arrive pre-validated against the endpoint's sort allowlist; Id keeps paging stable.</summary>
    private static IQueryable<Activity> ApplySort(IQueryable<Activity> query,
        IReadOnlyList<(string Field, bool Descending)> sort)
    {
        IOrderedQueryable<Activity>? ordered = null;
        foreach (var (field, descending) in sort)
        {
            Expression<Func<Activity, object>> key = field switch
            {
                "dueAt" => a => a.DueAt,
                "kind" => a => a.Kind,
                _ => throw new ArgumentOutOfRangeException(nameof(sort), field, "Unsupported sort field.")
            };
            ordered = ordered is null
                ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered is null ? query.OrderBy(a => a.Id) : ordered.ThenBy(a => a.Id);
    }
}

public sealed class GetActivityByIdHandler(AppDbContext db) : IRequestHandler<GetActivityByIdQuery, ActivitiesResult?>
{
    public async Task<ActivitiesResult?> Handle(GetActivityByIdQuery request, CancellationToken cancellationToken)
    {
        var activity = await db.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (activity is null)
        {
            return null;
        }

        return await ActivityIncludes.LoadAsync(db, [activity], 1, request.IncludeDeal, request.IncludeContact, cancellationToken);
    }
}

internal static class ActivityIncludes
{
    public static async Task<ActivitiesResult> LoadAsync(
        AppDbContext db, IReadOnlyList<Activity> activities, int total, bool includeDeal, bool includeContact,
        CancellationToken cancellationToken)
    {
        List<Deal>? deals = null;
        Dictionary<int, Dictionary<string, object?>>? dealFields = null;
        if (includeDeal)
        {
            var dealIds = activities.Where(a => a.DealId is not null).Select(a => a.DealId!.Value).Distinct().ToList();
            deals = await db.Deals.AsNoTracking()
                .Where(d => dealIds.Contains(d.Id)).OrderBy(d => d.Id).ToListAsync(cancellationToken);
            dealFields = await CustomFields.LoadAsync(db, "deals", dealIds, cancellationToken);
        }

        List<Contact>? contacts = null;
        Dictionary<int, Dictionary<string, object?>>? contactFields = null;
        if (includeContact)
        {
            var contactIds = activities.Where(a => a.ContactId is not null).Select(a => a.ContactId!.Value).Distinct().ToList();
            contacts = await db.Contacts.AsNoTracking()
                .Where(c => contactIds.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync(cancellationToken);
            contactFields = await CustomFields.LoadAsync(db, "contacts", contactIds, cancellationToken);
        }

        return new ActivitiesResult(activities, total, deals, dealFields, contacts, contactFields);
    }
}
