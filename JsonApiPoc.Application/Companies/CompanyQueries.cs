using System.Linq.Expressions;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Companies;

public sealed record CompaniesResult(IReadOnlyList<Company> Companies, int Total);

public sealed record GetCompaniesQuery(int PageNumber, int PageSize,
    IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<CompaniesResult>;

public sealed record GetCompanyByIdQuery(int Id) : IRequest<Company?>;

public sealed class GetCompaniesHandler(AppDbContext db) : IRequestHandler<GetCompaniesQuery, CompaniesResult>
{
    public async Task<CompaniesResult> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var total = await db.Companies.CountAsync(cancellationToken);
        var companies = await ApplySort(db.Companies.AsNoTracking(), request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new CompaniesResult(companies, total);
    }

    /// <summary>Field names arrive pre-validated against the endpoint's sort allowlist; Id keeps paging stable.</summary>
    private static IQueryable<Company> ApplySort(IQueryable<Company> query,
        IReadOnlyList<(string Field, bool Descending)> sort)
    {
        IOrderedQueryable<Company>? ordered = null;
        foreach (var (field, descending) in sort)
        {
            Expression<Func<Company, object>> key = field switch
            {
                "name" => c => c.Name,
                "industry" => c => c.Industry,
                _ => throw new ArgumentOutOfRangeException(nameof(sort), field, "Unsupported sort field.")
            };
            ordered = ordered is null
                ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered is null ? query.OrderBy(c => c.Id) : ordered.ThenBy(c => c.Id);
    }
}

public sealed class GetCompanyByIdHandler(AppDbContext db) : IRequestHandler<GetCompanyByIdQuery, Company?>
{
    public async Task<Company?> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken) =>
        await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
}
