using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealContact
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapGet("/{id:int}/contact", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(id, IncludeCompany: false, IncludeContact: true, IncludeOwner: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            // data stays null when the deal has no contact — an empty to-one, not a 404.
            ResourceObject? data = null;
            if (result.Contact is not null)
            {
                data = maps.Get<Contact>().Build(result.Contact, query, result.ContactFields);
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = data,
                Links = new JsonApiLinks(Self: $"/api/deals/{id}/contact")
            });
        })
        .WithJsonApiQuery(fieldsFor: ["contacts"], paging: false)
        .WithSummary("Get a deal's contact (related resource; data is null when unset). Supports fields[type].")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
