namespace JsonApiPoc.Endpoints.CustomFields;

public static class CustomFieldEndpoints
{
    public static IEndpointRouteBuilder MapCustomFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var customFields = app.MapGroup("/api/custom-fields").WithTags("Custom fields");

        ListCustomFields.Map(customFields);

        return app;
    }
}
