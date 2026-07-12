using JsonApiKit;
using JsonApiPoc.Domain;

namespace JsonApiPoc.JsonApi;

// Resource maps declaring how each entity serializes to a JSON:API resource object.
// Attribute/relationship names double as the spec "fields" served by fields[type]=...;
// custom-fields dictionaries are loaded separately and flow in as Build state.

public sealed class CompanyMap : ResourceMap<Company>
{
    public override string ResourceType => "companies";
    protected override string Id(Company company) => company.Id.ToString();

    public CompanyMap()
    {
        SelfLink(c => $"/api/companies/{c.Id}");
        Attribute("name", c => c.Name);
        Attribute("industry", c => c.Industry);
        Attribute("website", c => c.Website);
        // Links-only: advertise the related collection rather than loading every contact id into
        // each company of a list response. Related-only (no self) because the API serves no
        // /api/companies/{id}/relationships/contacts route.
        ToManyLinks("contacts");
    }
}

public sealed class ContactMap : ResourceMap<Contact>
{
    public override string ResourceType => "contacts";
    protected override string Id(Contact contact) => contact.Id.ToString();

    public ContactMap()
    {
        SelfLink(c => $"/api/contacts/{c.Id}");
        Attribute("firstName", c => c.FirstName);
        Attribute("lastName", c => c.LastName);
        Attribute("email", c => c.Email);
        Attribute("phone", c => c.Phone);
        Attribute("customFields", (_, state) => state);
        ToOne("company", "companies", c => c.CompanyId, links: true);
    }
}

public sealed class UserMap : ResourceMap<User>
{
    public override string ResourceType => "users";
    protected override string Id(User user) => user.Id.ToString();

    public UserMap()
    {
        SelfLink(u => $"/api/users/{u.Id}");
        Attribute("name", u => u.Name);
        Attribute("email", u => u.Email);
    }
}

public sealed class DealMap : ResourceMap<Deal>
{
    public override string ResourceType => "deals";
    protected override string Id(Deal deal) => deal.Id.ToString();

    public DealMap()
    {
        SelfLink(d => $"/api/deals/{d.Id}");
        Attribute("title", d => d.Title);
        Attribute("amount", d => d.Amount);
        Attribute("stage", d => d.Stage);
        Attribute("closeDate", d => d.CloseDate);
        Attribute("customFields", (_, state) => state);
        ToOne("company", "companies", d => d.CompanyId, links: true);
        ToOne("contact", "contacts", d => d.ContactId, links: true);
        ToOne("owner", "users", d => d.OwnerId, links: true);
    }
}

public sealed class ActivityMap : ResourceMap<Activity>
{
    public override string ResourceType => "activities";
    protected override string Id(Activity activity) => activity.Id.ToString();

    public ActivityMap()
    {
        SelfLink(a => $"/api/activities/{a.Id}");
        Attribute("kind", a => a.Kind);
        Attribute("subject", a => a.Subject);
        Attribute("dueAt", a => a.DueAt);
        Attribute("completed", a => a.Completed);
        ToOne("deal", "deals", a => a.DealId, links: true);
        ToOne("contact", "contacts", a => a.ContactId, links: true);
    }
}

public sealed class CustomFieldDefinitionMap : ResourceMap<CustomFieldDefinition>
{
    public override string ResourceType => "custom-fields";
    protected override string Id(CustomFieldDefinition definition) => definition.Id.ToString();

    public CustomFieldDefinitionMap()
    {
        // No SelfLink: the API has no GET /api/custom-fields/{id} route to point at.
        Attribute("resourceType", d => d.ResourceType);
        Attribute("key", d => d.Key);
        Attribute("label", d => d.Label);
        Attribute("dataType", d => d.DataType);
    }
}
