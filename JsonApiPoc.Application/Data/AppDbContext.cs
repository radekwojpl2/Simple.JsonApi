using JsonApiPoc.Domain;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Deleting a contact/deal must not take dependents down with it.
        modelBuilder.Entity<Deal>()
            .HasOne(d => d.Contact).WithMany().HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Deal).WithMany().HasForeignKey(a => a.DealId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.Contact).WithMany().HasForeignKey(a => a.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomFieldDefinition>()
            .HasIndex(d => new { d.ResourceType, d.Key }).IsUnique();
        modelBuilder.Entity<CustomFieldValue>()
            .HasIndex(v => new { v.ResourceType, v.ResourceId });
    }

    public void Seed()
    {
        if (Users.Any())
        {
            return;
        }

        var sarah = new User { Name = "Sarah Chen", Email = "sarah@crm.example.com" };
        var marcus = new User { Name = "Marcus Webb", Email = "marcus@crm.example.com" };
        Users.AddRange(sarah, marcus);

        var acme = new Company { Name = "Acme Manufacturing", Industry = "Manufacturing", Website = "https://acme.example.com" };
        var globex = new Company { Name = "Globex Logistics", Industry = "Transportation", Website = "https://globex.example.com" };
        var initech = new Company { Name = "Initech Software", Industry = "Technology", Website = "https://initech.example.com" };
        // Umbrella has no contacts and no deals — the empty-relationships case.
        var umbrella = new Company { Name = "Umbrella Health", Industry = "Healthcare", Website = "https://umbrella.example.com" };
        Companies.AddRange(acme, globex, initech, umbrella);

        var jan = new Contact { FirstName = "Jan", LastName = "Kowalski", Email = "jan.kowalski@acme.example.com", Phone = "+48 600 100 200", Company = acme };
        var maria = new Contact { FirstName = "Maria", LastName = "Nowak", Email = "maria.nowak@acme.example.com", Phone = "+48 600 300 400", Company = acme };
        var piotr = new Contact { FirstName = "Piotr", LastName = "Wisniewski", Email = "piotr@globex.example.com", Phone = "+48 700 500 600", Company = globex };
        var anna = new Contact { FirstName = "Anna", LastName = "Zielinska", Email = "anna@initech.example.com", Phone = "+48 800 700 800", Company = initech };
        // Adam shares Jan's last name, so single-field sorting by lastName alone is ambiguous.
        var adam = new Contact { FirstName = "Adam", LastName = "Kowalski", Email = "adam.kowalski@initech.example.com", Phone = "+48 900 100 300", Company = initech };
        Contacts.AddRange(jan, maria, piotr, anna, adam);

        var erpDeal = new Deal { Title = "Acme - ERP integration", Amount = 48000m, Stage = "proposal", CloseDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), Company = acme, Contact = jan, Owner = sarah };
        var fleetDeal = new Deal { Title = "Globex - fleet tracking pilot", Amount = 12500m, Stage = "qualified", CloseDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), Company = globex, Contact = piotr, Owner = marcus };
        var renewalDeal = new Deal { Title = "Initech - support renewal", Amount = 8000m, Stage = "won", CloseDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), Company = initech, Contact = anna, Owner = sarah };
        var hardwareDeal = new Deal { Title = "Acme - hardware upgrade", Amount = 30000m, Stage = "lead", Company = acme, Owner = marcus };
        var warehouseDeal = new Deal { Title = "Globex - warehouse expansion", Amount = 22000m, Stage = "lost", CloseDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), Company = globex, Contact = piotr, Owner = marcus };
        Deals.AddRange(erpDeal, fleetDeal, renewalDeal, hardwareDeal, warehouseDeal);

        Activities.AddRange(
            new Activity { Kind = "call", Subject = "Discovery call with Jan", DueAt = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc), Deal = erpDeal, Contact = jan },
            new Activity { Kind = "email", Subject = "Send proposal draft", DueAt = new DateTime(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc), Deal = erpDeal },
            new Activity { Kind = "meeting", Subject = "Pilot kickoff", DueAt = new DateTime(2026, 7, 15, 13, 0, 0, DateTimeKind.Utc), Deal = fleetDeal, Contact = piotr },
            new Activity { Kind = "task", Subject = "Prepare renewal quote", DueAt = new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc), Completed = true, Deal = renewalDeal, Contact = anna },
            // A housekeeping task tied to nothing — both relationships empty.
            new Activity { Kind = "task", Subject = "Quarterly CRM data cleanup", DueAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc) });

        var leadSource = new CustomFieldDefinition { ResourceType = "contacts", Key = "leadSource", Label = "Lead source", DataType = "text" };
        var newsletter = new CustomFieldDefinition { ResourceType = "contacts", Key = "newsletterOptIn", Label = "Newsletter opt-in", DataType = "boolean" };
        var probability = new CustomFieldDefinition { ResourceType = "deals", Key = "probability", Label = "Win probability (%)", DataType = "number" };
        var competitor = new CustomFieldDefinition { ResourceType = "deals", Key = "competitor", Label = "Main competitor", DataType = "text" };
        var contractSigned = new CustomFieldDefinition { ResourceType = "deals", Key = "contractSignedDate", Label = "Contract signed", DataType = "date" };
        CustomFieldDefinitions.AddRange(leadSource, newsletter, probability, competitor, contractSigned);

        SaveChanges();

        CustomFieldValues.AddRange(
            new CustomFieldValue { DefinitionId = leadSource.Id, ResourceType = "contacts", ResourceId = jan.Id, Value = "referral" },
            new CustomFieldValue { DefinitionId = newsletter.Id, ResourceType = "contacts", ResourceId = jan.Id, Value = "true" },
            new CustomFieldValue { DefinitionId = newsletter.Id, ResourceType = "contacts", ResourceId = maria.Id, Value = "false" },
            new CustomFieldValue { DefinitionId = leadSource.Id, ResourceType = "contacts", ResourceId = piotr.Id, Value = "webinar" },
            new CustomFieldValue { DefinitionId = probability.Id, ResourceType = "deals", ResourceId = erpDeal.Id, Value = "60" },
            new CustomFieldValue { DefinitionId = competitor.Id, ResourceType = "deals", ResourceId = erpDeal.Id, Value = "SAP" },
            new CustomFieldValue { DefinitionId = probability.Id, ResourceType = "deals", ResourceId = fleetDeal.Id, Value = "25" },
            new CustomFieldValue { DefinitionId = probability.Id, ResourceType = "deals", ResourceId = warehouseDeal.Id, Value = "12.5" },
            new CustomFieldValue { DefinitionId = competitor.Id, ResourceType = "deals", ResourceId = warehouseDeal.Id, Value = "FleetCo" },
            new CustomFieldValue { DefinitionId = contractSigned.Id, ResourceType = "deals", ResourceId = renewalDeal.Id, Value = "2026-06-28" });

        SaveChanges();
    }
}
