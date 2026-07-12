using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealById
{
    public static void Map(RouteGroupBuilder deals) =>
        deals.MapGet("/{id:int}", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(
                id, query.Has("company"), query.Has("contact"), query.Has("owner")));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            List<ResourceObject>? included = null;
            if (result.Company is not null)
            {
                included ??= [];
                included.Add(maps.Get<Company>().Build(result.Company, query));
            }
            if (result.Contact is not null)
            {
                included ??= [];
                included.Add(maps.Get<Contact>().Build(result.Contact, query, result.ContactFields));
            }
            if (result.Owner is not null)
            {
                included ??= [];
                included.Add(maps.Get<User>().Build(result.Owner, query));
            }

            var document = new JsonApiDocument
            {
                Data = maps.Get<Deal>().Build(result.Deal, query, result.CustomFields),
                Included = included
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(includes: ["company", "contact", "owner"],
            fieldsFor: ["deals", "companies", "contacts", "users"], paging: false)
        .WithSummary("Get a deal by id. Supports include=company,contact,owner and fields[type].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(404);
}
