using JsonApiKit;
using JsonApiPoc.Application.Activities;
using JsonApiPoc.Domain;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Activities;

public static class GetActivityContact
{
    public static void Map(RouteGroupBuilder activities) =>
        activities.MapGet("/{id:int}/contact", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetActivityByIdQuery(id, IncludeDeal: false, IncludeContact: true));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Activity '{id}' does not exist.");
            }

            // data stays null when the activity has no contact — an empty to-one, not a 404.
            ResourceObject? data = null;
            var contact = result.Contacts?.FirstOrDefault();
            if (contact is not null)
            {
                data = maps.Get<Contact>().Build(contact, query, result.ContactFields?.GetValueOrDefault(contact.Id));
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = data,
                Links = new JsonApiLinks(Self: $"/api/activities/{id}/contact")
            });
        })
        .WithJsonApiQuery(fieldsFor: ["contacts"], paging: false)
        .WithSummary("Get an activity's contact (related resource; data is null when unset). Supports fields[type].")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
