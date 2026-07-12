namespace JsonApiPoc.Endpoints.Contacts;

public static class ContactEndpoints
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var contacts = app.MapGroup("/api/contacts").WithTags("Contacts");

        ListContacts.Map(contacts);
        GetContactById.Map(contacts);
        GetContactCompany.Map(contacts);
        GetContactCompanyRelationship.Map(contacts);
        CreateContact.Map(contacts);
        UpdateContact.Map(contacts);
        DeleteContact.Map(contacts);

        return app;
    }
}
