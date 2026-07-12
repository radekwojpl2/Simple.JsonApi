using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Contacts;

public sealed record ContactsResult(
    IReadOnlyList<Contact> Contacts,
    int Total,
    IReadOnlyDictionary<int, Dictionary<string, object?>> CustomFields,
    IReadOnlyList<Company>? Companies);

public sealed record ContactResult(
    Contact Contact,
    Dictionary<string, object?>? CustomFields,
    Company? Company);

public sealed record GetContactsQuery(bool IncludeCompany, int PageNumber, int PageSize,
    IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<ContactsResult>;

public sealed record GetContactByIdQuery(int Id, bool IncludeCompany) : IRequest<ContactResult?>;

/// <summary>Contacts belonging to one company (the to-many related resource collection).
/// A null result means the company itself does not exist, as distinct from a company with no contacts.</summary>
public sealed record GetCompanyContactsQuery(int CompanyId, int PageNumber, int PageSize,
    IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<ContactsResult?>;

public sealed class GetContactsHandler(AppDbContext db) : IRequestHandler<GetContactsQuery, ContactsResult>
{
    public async Task<ContactsResult> Handle(GetContactsQuery request, CancellationToken cancellationToken)
    {
        var total = await db.Contacts.CountAsync(cancellationToken);
        var contacts = await ContactSort.Apply(db.Contacts.AsNoTracking(), request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var fields = await CustomFields.LoadAsync(db, "contacts", contacts.Select(c => c.Id).ToList(), cancellationToken);

        List<Company>? companies = null;
        if (request.IncludeCompany)
        {
            var companyIds = contacts.Select(c => c.CompanyId).Distinct().ToList();
            companies = await db.Companies.AsNoTracking()
                .Where(c => companyIds.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync(cancellationToken);
        }

        return new ContactsResult(contacts, total, fields, companies);
    }
}

public sealed class GetCompanyContactsHandler(AppDbContext db)
    : IRequestHandler<GetCompanyContactsQuery, ContactsResult?>
{
    public async Task<ContactsResult?> Handle(GetCompanyContactsQuery request, CancellationToken cancellationToken)
    {
        var companyExists = await db.Companies.AnyAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (!companyExists)
        {
            return null;
        }

        // Filtered on the foreign key rather than through the Company.Contacts navigation, which
        // would materialize every contact before paging.
        var scoped = db.Contacts.AsNoTracking().Where(c => c.CompanyId == request.CompanyId);
        var total = await scoped.CountAsync(cancellationToken);
        var contacts = await ContactSort.Apply(scoped, request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        var fields = await CustomFields.LoadAsync(db, "contacts", contacts.Select(c => c.Id).ToList(), cancellationToken);

        return new ContactsResult(contacts, total, fields, Companies: null);
    }
}

public sealed class GetContactByIdHandler(AppDbContext db) : IRequestHandler<GetContactByIdQuery, ContactResult?>
{
    public async Task<ContactResult?> Handle(GetContactByIdQuery request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contact is null)
        {
            return null;
        }

        var fields = await CustomFields.LoadAsync(db, "contacts", [contact.Id], cancellationToken);
        Company? company = null;
        if (request.IncludeCompany)
        {
            company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == contact.CompanyId, cancellationToken);
        }

        return new ContactResult(contact, fields.GetValueOrDefault(contact.Id), company);
    }
}
