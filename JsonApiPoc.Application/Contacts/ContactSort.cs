using System.Linq.Expressions;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Application.Contacts;

/// <summary>Ordering shared by the contacts collection and the company-contacts collection.
/// Field names arrive pre-validated against the endpoints' sort allowlist; Id keeps paging stable.</summary>
internal static class ContactSort
{
    public static IQueryable<Contact> Apply(IQueryable<Contact> query,
        IReadOnlyList<(string Field, bool Descending)> sort)
    {
        IOrderedQueryable<Contact>? ordered = null;
        foreach (var (field, descending) in sort)
        {
            Expression<Func<Contact, object>> key = field switch
            {
                "lastName" => c => c.LastName,
                "firstName" => c => c.FirstName,
                "email" => c => c.Email,
                _ => throw new ArgumentOutOfRangeException(nameof(sort), field, "Unsupported sort field.")
            };
            ordered = ordered is null
                ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered is null ? query.OrderBy(c => c.Id) : ordered.ThenBy(c => c.Id);
    }
}
