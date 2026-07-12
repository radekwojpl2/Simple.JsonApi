using JsonApiKit;
using JsonApiPoc.Application.CustomFieldDefinitions;
using JsonApiPoc.Domain;
using MediatR;

namespace JsonApiPoc.Endpoints.CustomFields;

public static class ListCustomFields
{
    // The field catalog itself is a JSON:API collection, filterable per resource type,
    // so clients can render dynamic forms for tenant-defined fields.
    public static void Map(RouteGroupBuilder customFields) =>
        customFields.MapGet("/", async (ISender sender, ResourceMapRegistry maps, JsonApiQuery query) =>
        {
            var result = await sender.Send(new GetCustomFieldDefinitionsQuery(
                query.Filter("resourceType"), query.Page.Number, query.Page.Size, query.Sort));

            var definitionMap = maps.Get<CustomFieldDefinition>();
            var document = new JsonApiDocument
            {
                Data = result.Definitions.Select(d => definitionMap.Build(d, query)).ToList(),
                Links = query.PageLinks(result.Total),
                Meta = query.PageMeta(result.Total)
            };
            return JsonApiResults.Ok(document);
        })
        .WithJsonApiQuery(
            sorts: ["key", "resourceType"],
            filters: new() { ["resourceType"] = ["contacts", "deals", "companies"] },
            fieldsFor: ["custom-fields"])
        .WithSummary("List custom field definitions. Supports filter[resourceType]=contacts|deals, sort, fields[type], and page[number]/page[size].")
        .Produces<JsonApiDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(400);
}
