namespace JsonApiPoc.Endpoints.Deals;

public static class DealEndpoints
{
    public static IEndpointRouteBuilder MapDealEndpoints(this IEndpointRouteBuilder app)
    {
        var deals = app.MapGroup("/api/deals").WithTags("Deals");

        ListDeals.Map(deals);
        GetDealById.Map(deals);
        GetDealCompany.Map(deals);
        GetDealCompanyRelationship.Map(deals);
        GetDealOwner.Map(deals);
        GetDealOwnerRelationship.Map(deals);
        GetDealContact.Map(deals);
        GetDealContactRelationship.Map(deals);
        CreateDeal.Map(deals);
        UpdateDeal.Map(deals);
        DeleteDeal.Map(deals);

        return app;
    }
}
