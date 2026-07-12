using JsonApiKit;
using JsonApiPoc.Application.Activities;
using JsonApiPoc.Domain;

namespace JsonApiPoc.Endpoints.Activities;

/// <summary>Shared by <see cref="ListActivities"/> and <see cref="GetActivityById"/> to build the
/// compound-document included resources from an <see cref="ActivitiesResult"/>.</summary>
internal static class ActivityIncluded
{
    internal static IReadOnlyList<ResourceObject>? Build(ActivitiesResult result, ResourceMapRegistry maps,
        JsonApiQuery query)
    {
        if (result.Deals is null && result.Contacts is null)
        {
            return null;
        }

        var included = new List<ResourceObject>();
        if (result.Deals is not null)
        {
            included.AddRange(result.Deals.Select(d =>
                maps.Get<Deal>().Build(d, query, result.DealFields?.GetValueOrDefault(d.Id))));
        }
        if (result.Contacts is not null)
        {
            included.AddRange(result.Contacts.Select(c =>
                maps.Get<Contact>().Build(c, query, result.ContactFields?.GetValueOrDefault(c.Id))));
        }

        return included;
    }
}
