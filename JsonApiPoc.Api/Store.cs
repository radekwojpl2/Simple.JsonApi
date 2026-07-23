namespace JsonApiPoc.Api;

public sealed class Contact
{
    public required string Id { get; init; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? CompanyId { get; set; }
    public List<string> TagIds { get; set; } = [];
}

public sealed class Company
{
    public required string Id { get; init; }
    public string? Name { get; set; }
}

public sealed class Tag
{
    public required string Id { get; init; }
    public string? Label { get; set; }
}

/// <summary>Mock data, in memory for the life of the process. Nothing here is the point — the
/// endpoints are.</summary>
public static class Store
{
    public static readonly List<Company> Companies =
    [
        new() { Id = "7", Name = "Acme" },
        new() { Id = "8", Name = "Initech" },
    ];

    public static readonly List<Tag> Tags =
    [
        new() { Id = "3", Label = "vip" },
        new() { Id = "9", Label = "newsletter" },
    ];

    public static readonly List<Contact> Contacts =
    [
        new() { Id = "1", FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", CompanyId = "7", TagIds = ["3"] },
        new() { Id = "2", FirstName = "Alan", LastName = "Turing", Email = "alan@example.com", CompanyId = "7", TagIds = ["3", "9"] },
        new() { Id = "3", FirstName = "Grace", LastName = "Hopper", Email = null, CompanyId = "8", TagIds = [] },
    ];

    private static int _lastContactId = 3;

    public static string NextContactId() =>
        Interlocked.Increment(ref _lastContactId).ToString();

    public static Contact? FindContact(string id) =>
        Contacts.FirstOrDefault(contact => contact.Id == id);

    public static Company? FindCompany(string id) =>
        Companies.FirstOrDefault(company => company.Id == id);
}
