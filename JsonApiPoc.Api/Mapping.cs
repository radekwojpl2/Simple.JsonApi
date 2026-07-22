using JsonApiLite;

namespace JsonApiPoc.Api;

/// <summary>Entities to resources. Relationships carry their own links so a client can follow
/// them without guessing URLs.</summary>
public static class Mapping
{
    public static Resource<ContactAttributes, ContactRelationships> ToResource(Contact contact) =>
        new()
        {
            Type = ContactAttributes.ResourceType,
            Id = contact.Id,
            Attributes = new ContactAttributes(contact.FirstName, contact.LastName, contact.Email),
            Relationships = new ContactRelationships
            {
                Company = CompanyOf(contact),
                Tags = Relationship.ToMany<TagAttributes>(contact.TagIds) with
                {
                    Links = new Links
                    {
                        Self = $"/contacts/{contact.Id}/relationships/tags",
                        Related = $"/contacts/{contact.Id}/tags",
                    },
                },
            },
            Links = new Links { Self = $"/contacts/{contact.Id}" },
        };

    public static Resource<CompanyAttributes, CompanyRelationships> ToResource(Company company)
    {
        var memberIds = Store.Contacts
            .Where(contact => contact.CompanyId == company.Id)
            .Select(contact => contact.Id);

        return new Resource<CompanyAttributes, CompanyRelationships>
        {
            Type = CompanyAttributes.ResourceType,
            Id = company.Id,
            Attributes = new CompanyAttributes(company.Name),
            Relationships = new CompanyRelationships
            {
                Contacts = Relationship.ToMany<ContactAttributes>(memberIds),
            },
            Links = new Links { Self = $"/companies/{company.Id}" },
        };
    }

    /// <summary>A to-one that is empty on the wire as <c>"data": null</c> — the spec's way of
    /// saying "this contact belongs to no company", which is not the same as omitting it.</summary>
    private static ToOneRelationship CompanyOf(Contact contact)
    {
        var links = new Links
        {
            Self = $"/contacts/{contact.Id}/relationships/company",
            Related = $"/contacts/{contact.Id}/company",
        };
        if (contact.CompanyId is null)
        {
            return Relationship.EmptyToOne() with { Links = links };
        }
        return Relationship.ToOne<CompanyAttributes>(contact.CompanyId) with { Links = links };
    }
}
