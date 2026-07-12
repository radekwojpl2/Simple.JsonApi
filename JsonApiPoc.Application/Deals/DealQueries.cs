using System.Linq.Expressions;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Deals;

public sealed record DealsResult(
    IReadOnlyList<Deal> Deals,
    int Total,
    IReadOnlyDictionary<int, Dictionary<string, object?>> DealFields,
    IReadOnlyList<Company>? Companies,
    IReadOnlyList<Contact>? Contacts,
    IReadOnlyDictionary<int, Dictionary<string, object?>>? ContactFields,
    IReadOnlyList<User>? Owners);

public sealed record DealResult(
    Deal Deal,
    Dictionary<string, object?>? CustomFields,
    Company? Company,
    Contact? Contact,
    Dictionary<string, object?>? ContactFields,
    User? Owner);

public sealed record GetDealsQuery(string? Stage, bool IncludeCompany, bool IncludeContact, bool IncludeOwner,
    int PageNumber, int PageSize, IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<DealsResult>;

public sealed record GetDealByIdQuery(int Id, bool IncludeCompany, bool IncludeContact, bool IncludeOwner)
    : IRequest<DealResult?>;

public sealed class GetDealsHandler(AppDbContext db) : IRequestHandler<GetDealsQuery, DealsResult>
{
    public async Task<DealsResult> Handle(GetDealsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Deals.AsNoTracking().AsQueryable();
        if (request.Stage is not null)
        {
            query = query.Where(d => d.Stage == request.Stage);
        }

        var total = await query.CountAsync(cancellationToken);
        var deals = await ApplySort(query, request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var dealFields = await CustomFields.LoadAsync(db, "deals", deals.Select(d => d.Id).ToList(), cancellationToken);

        List<Company>? companies = null;
        if (request.IncludeCompany)
        {
            var companyIds = deals.Select(d => d.CompanyId).Distinct().ToList();
            companies = await db.Companies.AsNoTracking()
                .Where(c => companyIds.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync(cancellationToken);
        }

        List<Contact>? contacts = null;
        Dictionary<int, Dictionary<string, object?>>? contactFields = null;
        if (request.IncludeContact)
        {
            var contactIds = deals.Where(d => d.ContactId is not null).Select(d => d.ContactId!.Value).Distinct().ToList();
            contacts = await db.Contacts.AsNoTracking()
                .Where(c => contactIds.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync(cancellationToken);
            contactFields = await CustomFields.LoadAsync(db, "contacts", contactIds, cancellationToken);
        }

        List<User>? owners = null;
        if (request.IncludeOwner)
        {
            var ownerIds = deals.Select(d => d.OwnerId).Distinct().ToList();
            owners = await db.Users.AsNoTracking()
                .Where(u => ownerIds.Contains(u.Id)).OrderBy(u => u.Id).ToListAsync(cancellationToken);
        }

        return new DealsResult(deals, total, dealFields, companies, contacts, contactFields, owners);
    }

    /// <summary>Field names arrive pre-validated against the endpoint's sort allowlist; Id keeps paging stable.</summary>
    private static IQueryable<Deal> ApplySort(IQueryable<Deal> query,
        IReadOnlyList<(string Field, bool Descending)> sort)
    {
        IOrderedQueryable<Deal>? ordered = null;
        foreach (var (field, descending) in sort)
        {
            Expression<Func<Deal, object>> key = field switch
            {
                "title" => d => d.Title,
                "amount" => d => d.Amount,
                "stage" => d => d.Stage,
                "closeDate" => d => d.CloseDate!,
                _ => throw new ArgumentOutOfRangeException(nameof(sort), field, "Unsupported sort field.")
            };
            ordered = ordered is null
                ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered is null ? query.OrderBy(d => d.Id) : ordered.ThenBy(d => d.Id);
    }
}

public sealed class GetDealByIdHandler(AppDbContext db) : IRequestHandler<GetDealByIdQuery, DealResult?>
{
    public async Task<DealResult?> Handle(GetDealByIdQuery request, CancellationToken cancellationToken)
    {
        var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (deal is null)
        {
            return null;
        }

        var fields = await CustomFields.LoadAsync(db, "deals", [deal.Id], cancellationToken);

        Company? company = null;
        if (request.IncludeCompany)
        {
            company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == deal.CompanyId, cancellationToken);
        }

        Contact? contact = null;
        Dictionary<string, object?>? contactFields = null;
        if (request.IncludeContact && deal.ContactId is { } contactId)
        {
            contact = await db.Contacts.AsNoTracking().FirstAsync(c => c.Id == contactId, cancellationToken);
            var loaded = await CustomFields.LoadAsync(db, "contacts", [contactId], cancellationToken);
            contactFields = loaded.GetValueOrDefault(contactId);
        }

        User? owner = null;
        if (request.IncludeOwner)
        {
            owner = await db.Users.AsNoTracking().FirstAsync(u => u.Id == deal.OwnerId, cancellationToken);
        }

        return new DealResult(deal, fields.GetValueOrDefault(deal.Id), company, contact, contactFields, owner);
    }
}
