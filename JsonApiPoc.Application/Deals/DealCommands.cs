using System.Text.Json;
using JsonApiPoc.Application.Common;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Deals;

public sealed record CreateDealCommand(
    string Title,
    decimal Amount,
    string Stage,
    DateTime? CloseDate,
    int CompanyId,
    int? ContactId,
    int OwnerId,
    Dictionary<string, JsonElement>? CustomFields) : IRequest<CommandResult<DealResult>>;

/// <summary>Partial update: null fields keep their current value; custom fields are merged into the existing set.</summary>
public sealed record UpdateDealCommand(
    int Id,
    string? Title,
    decimal? Amount,
    string? Stage,
    DateTime? CloseDate,
    int? CompanyId,
    int? ContactId,
    int? OwnerId,
    Dictionary<string, JsonElement>? CustomFields) : IRequest<CommandResult<DealResult>>;

public sealed record DeleteDealCommand(int Id) : IRequest<bool>;

/// <summary>Replaces the deal→contact linkage; a null ContactId clears it. The other deal
/// relationships are required, so their replacement goes through <see cref="UpdateDealCommand"/>.</summary>
public sealed record SetDealContactCommand(int Id, int? ContactId) : IRequest<CommandResult<Deal>>;

public sealed class CreateDealHandler(AppDbContext db) : IRequestHandler<CreateDealCommand, CommandResult<DealResult>>
{
    public async Task<CommandResult<DealResult>> Handle(CreateDealCommand request, CancellationToken cancellationToken)
    {
        if (!DealStages.All.Contains(request.Stage))
        {
            return CommandResult<DealResult>.Fail(422, "Validation failed",
                $"Unknown stage '{request.Stage}'. Valid stages: {string.Join(", ", DealStages.All)}.");
        }

        if (!await db.Companies.AnyAsync(c => c.Id == request.CompanyId, cancellationToken))
        {
            return CommandResult<DealResult>.Fail(404, "Not found",
                $"Company '{request.CompanyId}' does not exist.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.OwnerId, cancellationToken))
        {
            return CommandResult<DealResult>.Fail(404, "Not found",
                $"User '{request.OwnerId}' does not exist.");
        }

        if (request.ContactId is { } contactId && !await db.Contacts.AnyAsync(c => c.Id == contactId, cancellationToken))
        {
            return CommandResult<DealResult>.Fail(404, "Not found",
                $"Contact '{contactId}' does not exist.");
        }

        var (fieldError, convertedFields) =
            await Data.CustomFields.ValidateAsync(db, "deals", request.CustomFields, cancellationToken);
        if (fieldError is not null)
        {
            return new CommandResult<DealResult>(null, fieldError);
        }

        var deal = new Deal
        {
            Title = request.Title,
            Amount = request.Amount,
            Stage = request.Stage,
            CloseDate = request.CloseDate,
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            OwnerId = request.OwnerId
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync(cancellationToken);

        Data.CustomFields.Attach(db, "deals", deal.Id, convertedFields);
        await db.SaveChangesAsync(cancellationToken);

        var fields = await Data.CustomFields.LoadAsync(db, "deals", [deal.Id], cancellationToken);
        return CommandResult<DealResult>.Ok(new DealResult(deal, fields.GetValueOrDefault(deal.Id), null, null, null, null));
    }
}

public sealed class UpdateDealHandler(AppDbContext db) : IRequestHandler<UpdateDealCommand, CommandResult<DealResult>>
{
    public async Task<CommandResult<DealResult>> Handle(UpdateDealCommand request, CancellationToken cancellationToken)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (deal is null)
        {
            return CommandResult<DealResult>.Fail(404, "Not found", $"Deal '{request.Id}' does not exist.");
        }

        if (request.Stage is { } stage && !DealStages.All.Contains(stage))
        {
            return CommandResult<DealResult>.Fail(422, "Validation failed",
                $"Unknown stage '{stage}'. Valid stages: {string.Join(", ", DealStages.All)}.");
        }

        if (request.CompanyId is { } companyId && !await db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            return CommandResult<DealResult>.Fail(404, "Not found",
                $"Company '{companyId}' does not exist.");
        }

        if (request.OwnerId is { } ownerId && !await db.Users.AnyAsync(u => u.Id == ownerId, cancellationToken))
        {
            return CommandResult<DealResult>.Fail(404, "Not found",
                $"User '{ownerId}' does not exist.");
        }

        if (request.ContactId is { } contactId && !await db.Contacts.AnyAsync(c => c.Id == contactId, cancellationToken))
        {
            return CommandResult<DealResult>.Fail(404, "Not found",
                $"Contact '{contactId}' does not exist.");
        }

        var (fieldError, convertedFields) =
            await Data.CustomFields.ValidateAsync(db, "deals", request.CustomFields, cancellationToken);
        if (fieldError is not null)
        {
            return new CommandResult<DealResult>(null, fieldError);
        }

        deal.Title = request.Title ?? deal.Title;
        deal.Amount = request.Amount ?? deal.Amount;
        deal.Stage = request.Stage ?? deal.Stage;
        deal.CloseDate = request.CloseDate ?? deal.CloseDate;
        deal.CompanyId = request.CompanyId ?? deal.CompanyId;
        deal.ContactId = request.ContactId ?? deal.ContactId;
        deal.OwnerId = request.OwnerId ?? deal.OwnerId;

        await Data.CustomFields.UpsertAsync(db, "deals", deal.Id, convertedFields, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return CommandResult<DealResult>.Ok(new DealResult(deal, null, null, null, null, null));
    }
}

public sealed class SetDealContactHandler(AppDbContext db) : IRequestHandler<SetDealContactCommand, CommandResult<Deal>>
{
    public async Task<CommandResult<Deal>> Handle(SetDealContactCommand request, CancellationToken cancellationToken)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (deal is null)
        {
            return CommandResult<Deal>.Fail(404, "Not found", $"Deal '{request.Id}' does not exist.");
        }

        if (request.ContactId is { } contactId && !await db.Contacts.AnyAsync(c => c.Id == contactId, cancellationToken))
        {
            return CommandResult<Deal>.Fail(404, "Not found", $"Contact '{contactId}' does not exist.");
        }

        deal.ContactId = request.ContactId;
        await db.SaveChangesAsync(cancellationToken);
        return CommandResult<Deal>.Ok(deal);
    }
}

public sealed class DeleteDealHandler(AppDbContext db) : IRequestHandler<DeleteDealCommand, bool>
{
    public async Task<bool> Handle(DeleteDealCommand request, CancellationToken cancellationToken)
    {
        var deleted = await db.Deals.Where(d => d.Id == request.Id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return false;
        }

        await db.CustomFieldValues
            .Where(v => v.ResourceType == "deals" && v.ResourceId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);
        return true;
    }
}
