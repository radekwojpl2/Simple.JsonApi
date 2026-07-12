namespace JsonApiPoc.Endpoints.Companies;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var companies = app.MapGroup("/api/companies").WithTags("Companies");

        ListCompanies.Map(companies);
        GetCompanyById.Map(companies);
        GetCompanyContacts.Map(companies);

        return app;
    }
}
