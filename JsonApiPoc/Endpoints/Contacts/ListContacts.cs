using JsonApiKit;
using JsonApiPoc.Application.Contacts;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.Contacts;

public static class ListContacts
{
    public static void Map(RouteGroupBuilder contacts) =>
        contacts.MapGet("/", async (ISender sender, ResourceMapRegistry maps, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetContactsQuery(
                query.Has("company"), query.Page.Number, query.Page.Size, query.Sort));

            var contactMap = maps.Get<Contact>();
            var document = new JsonApiDocument
            {
                Data = result.Contacts
                    .Select(c => contactMap.Build(c, query, result.CustomFields.GetValueOrDefault(c.Id))).ToList(),
                Included = result.Companies?.Select(c => maps.Get<Company>().Build(c, query)).ToList(),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(includes: ["company"], sorts: ["lastName", "firstName", "email"],
            fieldsFor: ["contacts", "companies"])
        .WithSummary("List contacts. Supports include=company, sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400);
}
