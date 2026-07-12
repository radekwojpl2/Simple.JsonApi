using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealCompanyRelationship
{
    public static void Map(RouteGroupBuilder deals)
    {
        deals.MapGet("/{id:int}/relationships/company", async (ISender sender, int id) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(id, IncludeCompany: false, IncludeContact: false, IncludeOwner: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = new ResourceIdentifier("companies", result.Deal.CompanyId.ToString()),
                Links = new JsonApiLinks(
                    Self: $"/api/deals/{id}/relationships/company",
                    Related: $"/api/deals/{id}/company")
            });
        })
        .WithSummary("Get the deal→company relationship linkage (resource identifier only).")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);

        // Spec to-one relationship update (https://jsonapi.org/format/#crud-updating-to-one-relationships).
        // Every deal belongs to a company, so clearing (data: null) is rejected.
        deals.MapPatch("/{id:int}/relationships/company", async (ISender sender, int id, JsonNode? body) =>
        {
            if (ToOneLinkage.TryParse(body, "companies", out var targetId) is { } invalid)
            {
                return invalid;
            }
            if (targetId is null)
            {
                return JsonApiResults.Validation("The 'company' relationship is required and cannot be cleared.");
            }
            if (!int.TryParse(targetId, out var companyId))
            {
                return JsonApiResults.NotFound($"Company '{targetId}' does not exist.");
            }

            var result = await sender.Send(new UpdateDealCommand(id, Title: null, Amount: null,
                Stage: null, CloseDate: null, CompanyId: companyId, ContactId: null, OwnerId: null,
                CustomFields: null));
            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            return Results.NoContent();
        })
        .WithSummary("Replace the deal→company relationship from a to-one linkage document: {\"data\":{\"type\":\"companies\",\"id\":\"5\"}}. The relationship is required, so data:null is rejected. Returns 204.")
        .WithToOneLinkageBody("companies", clearable: false)
        .Produces(204)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesProblem(422);
    }
}
