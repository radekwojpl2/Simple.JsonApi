using System.Text.Json.Nodes;
using JsonApiKit;
using JsonApiPoc.Application.Deals;
using JsonApiPoc.JsonApi;
using MediatR;

namespace JsonApiPoc.Endpoints.Deals;

public static class GetDealOwnerRelationship
{
    public static void Map(RouteGroupBuilder deals)
    {
        deals.MapGet("/{id:int}/relationships/owner", async (ISender sender, int id) =>
        {
            var result = await sender.Send(new GetDealByIdQuery(id, IncludeCompany: false, IncludeContact: false, IncludeOwner: false));
            if (result is null)
            {
                return JsonApiResults.NotFound($"Deal '{id}' does not exist.");
            }

            return JsonApiResults.Ok(new JsonApiToOneDocument
            {
                Data = new ResourceIdentifier("users", result.Deal.OwnerId.ToString()),
                Links = new JsonApiLinks(
                    Self: $"/api/deals/{id}/relationships/owner",
                    Related: $"/api/deals/{id}/owner")
            });
        })
        .WithSummary("Get the deal→owner relationship linkage (resource identifier only).")
        .Produces<JsonApiToOneDocument>(contentType: JsonApiResults.MediaType)
        .ProducesProblem(404);

        // Spec to-one relationship update (https://jsonapi.org/format/#crud-updating-to-one-relationships).
        // Every deal has an owner, so clearing (data: null) is rejected.
        deals.MapPatch("/{id:int}/relationships/owner", async (ISender sender, int id, JsonNode? body) =>
        {
            if (ToOneLinkage.TryParse(body, "users", out var targetId) is { } invalid)
            {
                return invalid;
            }
            if (targetId is null)
            {
                return JsonApiResults.Validation("The 'owner' relationship is required and cannot be cleared.");
            }
            if (!int.TryParse(targetId, out var ownerId))
            {
                return JsonApiResults.NotFound($"User '{targetId}' does not exist.");
            }

            var result = await sender.Send(new UpdateDealCommand(id, Title: null, Amount: null,
                Stage: null, CloseDate: null, CompanyId: null, ContactId: null, OwnerId: ownerId,
                CustomFields: null));
            if (result.Error is { } error)
            {
                return JsonApiResults.Error(new JsonApiError { StatusCode = error.Status, Title = error.Title, Detail = error.Detail });
            }

            return Results.NoContent();
        })
        .WithSummary("Replace the deal→owner relationship from a to-one linkage document: {\"data\":{\"type\":\"users\",\"id\":\"5\"}}. The relationship is required, so data:null is rejected. Returns 204.")
        .WithToOneLinkageBody("users", clearable: false)
        .Produces(204)
        .ProducesProblem(400)
        .ProducesProblem(404)
        .ProducesProblem(409)
        .ProducesProblem(422);
    }
}
