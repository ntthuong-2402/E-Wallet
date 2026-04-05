using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record GetUsersQuery() : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler(IUserRepository users)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(
        GetUsersQuery request,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<Domain.Aggregates.UserAggregate.User> items = await users.ListAsync(
            cancellationToken
        );
        return items.Select(UserDto.FromUser).ToArray();
    }
}
