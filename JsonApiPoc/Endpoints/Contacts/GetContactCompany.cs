using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using JsonApiPoc.Domain;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

public static class GetContactCompany
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapGet("/{id:int}/company", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetContactByIdQuery(id, IncludeCompany: true));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Contact '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = maps.Get<Company>().Build(result.Company!, query),
                Links = new JsonApiLinks(Self: $"/api/contacts/{id}/company")
            });
        })
        .WithJsonApiQuery(fieldsFor: ["companies"], paging: false)
        .WithSummary("Get the company a contact belongs to (related resource). Supports fields[type].")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);
}
