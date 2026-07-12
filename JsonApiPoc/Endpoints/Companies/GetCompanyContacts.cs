using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Companies;

public static class GetCompanyContacts
{
    public static void Map(RouteGroupBuilder companies) =>
        companies.MapGet("/{id:int}/contacts", async (ISender sender, ResourceMapRegistry maps,
            int id, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetCompanyContactsQuery(
                id, query.Page.Number, query.Page.Size, query.Sort));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Company '{id}' does not exist.");
            }

            var contactMap = maps.Get<Contact>();
            var document = new JsonApiDocument
            {
                Data = result.Contacts
                    .Select(c => contactMap.Build(c, query, result.CustomFields.GetValueOrDefault(c.Id))).ToList(),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(sorts: ["lastName", "firstName", "email"], fieldsFor: ["contacts"])
        .WithSummary("List the contacts belonging to a company (related resource collection). Supports sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400)
        .ProducesProblem(404);
}
