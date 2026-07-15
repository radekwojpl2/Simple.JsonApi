using JsonApiKit;
using JsonApiKit.OpenApi;
using JsonApiPoc.Application;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Endpoints.Activities;
using JsonApiPoc.Endpoints.Companies;
using JsonApiPoc.Endpoints.Contacts;
using JsonApiPoc.Endpoints.CustomFields;
using JsonApiPoc.Endpoints.Deals;
using JsonApiPoc.Endpoints.Users;
using JsonApiPoc.JsonApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(o => o
    .AddJsonApiQueryParameters()
    .AddJsonApiLinkageBodies()
    .AddJsonApiResourceDocumentBodies());
builder.Services.AddProblemDetails();
builder.Services.AddApplication(builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddJsonApi(o => o
    .AddMap<CompanyMap>()
    .AddMap<ContactMap>()
    .AddMap<UserMap>()
    .AddMap<DealMap>()
    .AddMap<ActivityMap>()
    .AddMap<CustomFieldDefinitionMap>());

var app = builder.Build();

// Framework-generated errors (unmatched routes, malformed bodies, unhandled exceptions)
// come back as RFC 7807 problem details, same as the endpoints' own errors.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Malformed request bodies throw BadHttpRequestException; keep their 400 instead of a blanket 500.
    StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();
app.UseJsonApiContentNegotiation();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CRM JSON:API v1");
    });
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    db.Seed();
}

app.MapCompanyEndpoints();
app.MapContactEndpoints();
app.MapUserEndpoints();
app.MapDealEndpoints();
app.MapActivityEndpoints();
app.MapCustomFieldEndpoints();

app.Run();

// Exposes the entry point to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
