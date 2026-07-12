using JsonApiPoc.Domain;

namespace JsonApiPoc.IntegrationTests.Infrastructure;

/// <summary>Factory methods for the rows tests arrange. Every argument a test's assertions depend
/// on is a parameter; everything else gets a harmless default, so the arrange block reads as the
/// test's premises and nothing more.</summary>
public static class Rows
{
    public static Company Company(string name = "Acme Manufacturing", string industry = "Manufacturing") =>
        new() { Name = name, Industry = industry, Website = $"https://{name.Split(' ')[0].ToLowerInvariant()}.example.com" };

    public static User User(string name = "Sarah Chen") =>
        new() { Name = name, Email = $"{name.Split(' ')[0].ToLowerInvariant()}@crm.example.com" };

    public static Contact Contact(string firstName, string lastName, Company company, string? email = null) =>
        new()
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email ?? $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com",
            Company = company
        };

    public static Deal Deal(string title, Company company, User owner, decimal amount = 1000m,
        string stage = DealStages.Lead, Contact? contact = null, DateTime? closeDate = null) =>
        new()
        {
            Title = title,
            Amount = amount,
            Stage = stage,
            CloseDate = closeDate,
            Company = company,
            Owner = owner,
            Contact = contact
        };

    public static Activity Activity(string subject, string kind = "task", DateTime dueAt = default,
        bool completed = false, Deal? deal = null, Contact? contact = null) =>
        new()
        {
            Subject = subject,
            Kind = kind,
            DueAt = DateTime.SpecifyKind(dueAt, DateTimeKind.Utc),
            Completed = completed,
            Deal = deal,
            Contact = contact
        };

    public static CustomFieldDefinition Field(string resourceType, string key, string dataType = "text",
        string? label = null) =>
        new() { ResourceType = resourceType, Key = key, DataType = dataType, Label = label ?? key };

    /// <summary>The value store references resources by raw id, so the definition and the target
    /// resource must be saved (ids assigned) before this row is added — call db.SaveChanges() in
    /// the arrange block first.</summary>
    public static CustomFieldValue Value(CustomFieldDefinition definition, int resourceId, string value) =>
        new()
        {
            Definition = definition,
            ResourceType = definition.ResourceType,
            ResourceId = resourceId,
            Value = value
        };
}
