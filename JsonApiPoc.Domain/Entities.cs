namespace JsonApiPoc.Domain;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Website { get; set; } = "";
    public List<Contact> Contacts { get; set; } = [];
    public List<Deal> Deals { get; set; } = [];
}

public class Contact
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
}

/// <summary>A CRM user who owns deals (sales rep).</summary>
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class Deal
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public string Stage { get; set; } = DealStages.Lead;
    public DateTime? CloseDate { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public int OwnerId { get; set; }
    public User? Owner { get; set; }
}

public static class DealStages
{
    public const string Lead = "lead";
    public static readonly string[] All = [Lead, "qualified", "proposal", "won", "lost"];
}

public class Activity
{
    public int Id { get; set; }
    /// <summary>call | email | meeting | task</summary>
    public string Kind { get; set; } = "task";
    public string Subject { get; set; } = "";
    public DateTime DueAt { get; set; }
    public bool Completed { get; set; }
    public int? DealId { get; set; }
    public Deal? Deal { get; set; }
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
}

/// <summary>Tenant-defined field schema for a resource type (e.g. a "leadSource" text field on contacts).</summary>
public class CustomFieldDefinition
{
    public int Id { get; set; }
    /// <summary>JSON:API resource type the field belongs to: "contacts" | "deals" | "companies".</summary>
    public string ResourceType { get; set; } = "";
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>text | number | boolean | date</summary>
    public string DataType { get; set; } = "text";
}

/// <summary>Polymorphic value store: one row per (definition, resource instance). Values are kept as strings and typed on read.</summary>
public class CustomFieldValue
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public CustomFieldDefinition? Definition { get; set; }
    public string ResourceType { get; set; } = "";
    public int ResourceId { get; set; }
    public string Value { get; set; } = "";
}
