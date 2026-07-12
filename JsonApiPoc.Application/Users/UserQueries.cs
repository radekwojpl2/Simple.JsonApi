using System.Linq.Expressions;
using JsonApiPoc.Application.Data;
using JsonApiPoc.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JsonApiPoc.Application.Users;

public sealed record UsersResult(IReadOnlyList<User> Users, int Total);

public sealed record GetUsersQuery(int PageNumber, int PageSize,
    IReadOnlyList<(string Field, bool Descending)> Sort) : IRequest<UsersResult>;

public sealed record GetUserByIdQuery(int Id) : IRequest<User?>;

public sealed class GetUsersHandler(AppDbContext db) : IRequestHandler<GetUsersQuery, UsersResult>
{
    public async Task<UsersResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var total = await db.Users.CountAsync(cancellationToken);
        var users = await ApplySort(db.Users.AsNoTracking(), request.Sort)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new UsersResult(users, total);
    }

    /// <summary>Field names arrive pre-validated against the endpoint's sort allowlist; Id keeps paging stable.</summary>
    private static IQueryable<User> ApplySort(IQueryable<User> query,
        IReadOnlyList<(string Field, bool Descending)> sort)
    {
        IOrderedQueryable<User>? ordered = null;
        foreach (var (field, descending) in sort)
        {
            Expression<Func<User, object>> key = field switch
            {
                "name" => u => u.Name,
                "email" => u => u.Email,
                _ => throw new ArgumentOutOfRangeException(nameof(sort), field, "Unsupported sort field.")
            };
            ordered = ordered is null
                ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
                : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        }

        return ordered is null ? query.OrderBy(u => u.Id) : ordered.ThenBy(u => u.Id);
    }
}

public sealed class GetUserByIdHandler(AppDbContext db) : IRequestHandler<GetUserByIdQuery, User?>
{
    public async Task<User?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
}
