using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

public static class GetContactById
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapGet("/{id:int}", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetContactByIdQuery(id, query.Has("company")));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Contact '{id}' does not exist.");
            }

            List<ResourceObject>? included = null;
            if (result.Company is not null)
            {
                included = [maps.Get<Company>().Build(result.Company, query)];
            }

            var document = new JsonApiDocument
            {
                Data = maps.Get<Contact>().Build(result.Contact, query, result.CustomFields),
                Included = included
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(includes: ["company"], fieldsFor: ["contacts", "companies"], paging: false)
        .WithSummary("Get a contact by id. Supports include=company and fields[type].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(404);
}
