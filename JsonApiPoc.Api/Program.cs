using System.Text.Json;
using JsonApiLite;
using JsonApiPoc.Api;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Let minimal APIs bind JSON:API documents as handler parameters. Their body binding uses these
// options, so they have to match the ones the document types are designed for. TypeInfoResolver
// is the load-bearing line: it carries the rule that omits unset Optional<T> members.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    var source = JsonApiSerializer.Options;
    var target = options.SerializerOptions;
    target.PropertyNamingPolicy = source.PropertyNamingPolicy;
    target.PropertyNameCaseInsensitive = source.PropertyNameCaseInsensitive;
    target.NumberHandling = source.NumberHandling;
    target.DefaultIgnoreCondition = source.DefaultIgnoreCondition;
    target.TypeInfoResolver = source.TypeInfoResolver;
    foreach (var converter in source.Converters)
    {
        target.Converters.Add(converter);
    }
});

var app = builder.Build();

// A body the framework cannot bind throws during binding, before any handler runs. Without this
// the client would get a plain 400; JSON:API callers expect an errors document.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var thrown = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (thrown is JsonException)
    {
        await JsonApi.Malformed(thrown.Message).ExecuteAsync(context);
        return;
    }
    await JsonApi.ServerError().ExecuteAsync(context);
}));

// ---------------------------------------------------------------------------------------------
// GET a collection. Pagination is a query concern the library does not model, so the page is cut
// here and reported back as links plus a meta shape this endpoint declares.
// ---------------------------------------------------------------------------------------------
app.MapGet("/contacts", (int? pageNumber, int? pageSize) =>
{
    var number = pageNumber ?? 1;
    var size = pageSize ?? 2;
    var total = Store.Contacts.Count;
    var pageCount = (int)Math.Ceiling(total / (double)size);

    var page = Store.Contacts.Skip((number - 1) * size).Take(size).Select(Mapping.ToResource);

    var document = new ResourceCollectionDocument<ContactAttributes, ContactRelationships, PageMeta>
    {
        Data = [.. page],
        Links = new Links
        {
            Self = $"/contacts?pageNumber={number}&pageSize={size}",
            First = $"/contacts?pageNumber=1&pageSize={size}",
            Last = $"/contacts?pageNumber={pageCount}&pageSize={size}",
            Next = number < pageCount ? $"/contacts?pageNumber={number + 1}&pageSize={size}" : null,
            Prev = number > 1 ? $"/contacts?pageNumber={number - 1}&pageSize={size}" : null,
        },
        Meta = new PageMeta(Total: total, PageCount: pageCount),
    };

    return JsonApi.Ok(document);
});

// ---------------------------------------------------------------------------------------------
// GET one resource, optionally with its company sideloaded into 'included'.
// ---------------------------------------------------------------------------------------------
app.MapGet("/contacts/{id}", (string id, string? include) =>
{
    var contact = Store.FindContact(id);
    if (contact is null)
    {
        return JsonApi.NotFound($"No contact with id '{id}'.");
    }

    var included = new List<Resource>();
    if (include == "company" && contact.CompanyId is not null)
    {
        var company = Store.FindCompany(contact.CompanyId);
        if (company is not null)
        {
            included.Add(Mapping.ToResource(company));
        }
    }

    var document = new ResourceDocument<ContactAttributes, ContactRelationships>
    {
        Data = Mapping.ToResource(contact),
        Included = included.Count > 0 ? included : null,
        Links = new Links { Self = $"/contacts/{id}" },
    };

    return JsonApi.Ok(document);
});

// ---------------------------------------------------------------------------------------------
// POST a create. The client sends no id; the server assigns one and answers 201 with a Location.
// ---------------------------------------------------------------------------------------------
app.MapPost("/contacts", (
    ResourceDocument<ContactAttributes, ContactRelationships>? request, HttpContext http) =>
{
    if (request?.Data is null)
    {
        return JsonApi.Invalid("/data", "A create request must carry a resource object.");
    }
    if (string.IsNullOrWhiteSpace(request.Data.Attributes?.FirstName))
    {
        return JsonApi.Invalid("/data/attributes/firstName", "The firstName attribute is required.");
    }

    var contact = new Contact
    {
        Id = Store.NextContactId(),
        FirstName = request.Data.Attributes.FirstName,
        LastName = request.Data.Attributes.LastName,
        Email = request.Data.Attributes.Email,
        CompanyId = request.Data.Relationships?.Company?.Data?.Id,
        TagIds = [.. request.Data.Relationships?.Tags?.Data.Select(tag => tag.Id) ?? []],
    };
    Store.Contacts.Add(contact);

    var document = new ResourceDocument<ContactAttributes, ContactRelationships>
    {
        Data = Mapping.ToResource(contact),
        Links = new Links { Self = $"/contacts/{contact.Id}" },
    };

    http.Response.Headers.Location = $"/contacts/{contact.Id}";
    return JsonApi.Created(document);
});

// ---------------------------------------------------------------------------------------------
// PATCH an update, where the tri-state earns its keep: an attribute the body omits keeps its
// current value, an explicit null clears it, and the same holds one level up for relationships.
// ---------------------------------------------------------------------------------------------
app.MapPatch("/contacts/{id}", (
    string id, ResourceDocument<ContactPatchAttributes, ContactRelationships>? request) =>
{
    var contact = Store.FindContact(id);
    if (contact is null)
    {
        return JsonApi.NotFound($"No contact with id '{id}'.");
    }

    if (request?.Data is null)
    {
        return JsonApi.Invalid("/data", "An update request must carry a resource object.");
    }
    if (request.Data.Id is not null && request.Data.Id != id)
    {
        return JsonApi.Invalid("/data/id", "The id in the document must match the id in the URL.");
    }

    var patch = request.Data.Attributes;
    if (patch is not null)
    {
        if (patch.FirstName.IsSet) { contact.FirstName = patch.FirstName.Value; }
        if (patch.LastName.IsSet) { contact.LastName = patch.LastName.Value; }
        if (patch.Email.IsSet) { contact.Email = patch.Email.Value; }
    }

    var relationships = request.Data.Relationships;
    if (relationships?.Company is not null)
    {
        // Present with null data means "clear"; present with an identifier means "repoint".
        contact.CompanyId = relationships.Company.Data?.Id;
    }
    if (relationships?.Tags is not null)
    {
        // An empty array replaces the set with nothing.
        contact.TagIds = [.. relationships.Tags.Data.Select(tag => tag.Id)];
    }

    var document = new ResourceDocument<ContactAttributes, ContactRelationships>
    {
        Data = Mapping.ToResource(contact),
        Links = new Links { Self = $"/contacts/{id}" },
    };

    return JsonApi.Ok(document);
});

// ---------------------------------------------------------------------------------------------
// DELETE. No body either way, so nothing here is JSON:API except the 404.
// ---------------------------------------------------------------------------------------------
app.MapDelete("/contacts/{id}", (string id) =>
{
    var contact = Store.FindContact(id);
    if (contact is null)
    {
        return JsonApi.NotFound($"No contact with id '{id}'.");
    }

    Store.Contacts.Remove(contact);
    return Results.NoContent();
});

// ---------------------------------------------------------------------------------------------
// Relationship endpoints: linkage only, as ToOneLinkageDocument / ToManyLinkageDocument.
// ---------------------------------------------------------------------------------------------
app.MapGet("/contacts/{id}/relationships/company", (string id) =>
{
    var contact = Store.FindContact(id);
    if (contact is null)
    {
        return JsonApi.NotFound($"No contact with id '{id}'.");
    }

    ResourceIdentifier? data = null;
    if (contact.CompanyId is not null)
    {
        data = ResourceIdentifier.Of<CompanyAttributes>(contact.CompanyId);
    }

    return JsonApi.Ok(new ToOneLinkageDocument
    {
        Data = data,
        Links = new Links
        {
            Self = $"/contacts/{id}/relationships/company",
            Related = $"/contacts/{id}/company",
        },
    });
});

app.MapPatch("/contacts/{id}/relationships/company", (string id, ToOneLinkageDocument? request) =>
{
    var contact = Store.FindContact(id);
    if (contact is null)
    {
        return JsonApi.NotFound($"No contact with id '{id}'.");
    }

    if (request is null)
    {
        return JsonApi.Invalid("/data", "The body must be a to-one linkage document.");
    }

    // data: null clears the relationship, an identifier repoints it.
    contact.CompanyId = request.Data?.Id;
    return Results.NoContent();
});

app.MapGet("/contacts/{id}/company", (string id) =>
{
    var contact = Store.FindContact(id);
    if (contact is null)
    {
        return JsonApi.NotFound($"No contact with id '{id}'.");
    }

    Company? company = null;
    if (contact.CompanyId is not null)
    {
        company = Store.FindCompany(contact.CompanyId);
    }
    if (company is null)
    {
        // A related endpoint for an empty to-one answers 200 with null data, not 404.
        return JsonApi.Ok(new ResourceDocument<CompanyAttributes, CompanyRelationships>
        {
            Data = null,
            Links = new Links { Self = $"/contacts/{id}/company" },
        });
    }

    return JsonApi.Ok(new ResourceDocument<CompanyAttributes, CompanyRelationships>
    {
        Data = Mapping.ToResource(company),
        Links = new Links { Self = $"/contacts/{id}/company" },
    });
});

app.MapGet("/companies/{id}", (string id) =>
{
    var company = Store.FindCompany(id);
    if (company is null)
    {
        return JsonApi.NotFound($"No company with id '{id}'.");
    }

    return JsonApi.Ok(new ResourceDocument<CompanyAttributes, CompanyRelationships>
    {
        Data = Mapping.ToResource(company),
        Links = new Links { Self = $"/companies/{id}" },
    });
});

app.Run();
