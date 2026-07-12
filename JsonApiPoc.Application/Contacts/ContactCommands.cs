using System.Text.Json;
using JsonApiPoc.Application.Common;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Contacts;

public sealed record CreateContactCommand(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    int CompanyId,
    Dictionary<string, JsonElement>? CustomFields) : IRequest<CommandResult<ContactResult>>;

/// <summary>Partial update: null fields keep their current value; custom fields are merged into the existing set.</summary>
public sealed record UpdateContactCommand(
    int Id,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    int? CompanyId,
    Dictionary<string, JsonElement>? CustomFields) : IRequest<CommandResult<ContactResult>>;

public sealed record DeleteContactCommand(int Id) : IRequest<bool>;

public sealed class CreateContactHandler(AppDbContext db) : IRequestHandler<CreateContactCommand, CommandResult<ContactResult>>
{
    public async Task<CommandResult<ContactResult>> Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == request.CompanyId, cancellationToken))
        {
            return CommandResult<ContactResult>.Fail(404, "Not found",
                $"Company '{request.CompanyId}' does not exist.");
        }

        var (fieldError, convertedFields) =
            await Data.CustomFields.ValidateAsync(db, "contacts", request.CustomFields, cancellationToken);
        if (fieldError is not null)
        {
            return new CommandResult<ContactResult>(null, fieldError);
        }

        var contact = new Contact
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            CompanyId = request.CompanyId
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);

        Data.CustomFields.Attach(db, "contacts", contact.Id, convertedFields);
        await db.SaveChangesAsync(cancellationToken);

        var fields = await Data.CustomFields.LoadAsync(db, "contacts", [contact.Id], cancellationToken);
        return CommandResult<ContactResult>.Ok(new ContactResult(contact, fields.GetValueOrDefault(contact.Id), null));
    }
}

public sealed class UpdateContactHandler(AppDbContext db) : IRequestHandler<UpdateContactCommand, CommandResult<ContactResult>>
{
    public async Task<CommandResult<ContactResult>> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contact is null)
        {
            return CommandResult<ContactResult>.Fail(404, "Not found", $"Contact '{request.Id}' does not exist.");
        }

        if (request.CompanyId is { } companyId && !await db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return CommandResult<ContactResult>.Fail(404, "Not found",
                $"Company '{companyId}' does not exist.");
        }

        var (fieldError, convertedFields) =
            await Data.CustomFields.ValidateAsync(db, "contacts", request.CustomFields, cancellationToken);
        if (fieldError is not null)
        {
            return new CommandResult<ContactResult>(null, fieldError);
        }

        contact.FirstName = request.FirstName ?? contact.FirstName;
        contact.LastName = request.LastName ?? contact.LastName;
        contact.Email = request.Email ?? contact.Email;
        contact.Phone = request.Phone ?? contact.Phone;
        contact.CompanyId = request.CompanyId ?? contact.CompanyId;

        await Data.CustomFields.UpsertAsync(db, "contacts", contact.Id, convertedFields, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return CommandResult<ContactResult>.Ok(new ContactResult(contact, null, null));
    }
}

public sealed class DeleteContactHandler(AppDbContext db) : IRequestHandler<DeleteContactCommand, bool>
{
    public async Task<bool> Handle(DeleteContactCommand request, CancellationToken cancellationToken)
    {
        var deleted = await db.Contacts.Where(c => c.Id == request.Id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return false;
        }

        await db.CustomFieldValues
            .Where(v => v.ResourceType == "contacts" && v.ResourceId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);
        return true;
    }
}
