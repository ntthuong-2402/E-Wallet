using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record GetUsersQuery(
    int Skip = 0,
    int Take = 50,
    string? Search = null,
    bool? IsActive = null,
    bool? IsLocked = null,
    int? RoleId = null
) : IQuery<UserListPageDto>;

public sealed record UserListPageDto(IReadOnlyList<UserDto> Items, int TotalCount, int Skip, int Take);

public sealed class GetUsersHandler(IUserRepository users)
    : IQueryHandler<GetUsersQuery, UserListPageDto>
{
    public async Task<UserListPageDto> HandleAsync(
        GetUsersQuery request,
        CancellationToken cancellationToken
    )
    {
        int skip = Math.Max(0, request.Skip);
        int take = Math.Clamp(request.Take, 1, 200);
        UserListPage page = await users.ListAsync(
            skip,
            take,
            request.Search,
            request.IsActive,
            request.IsLocked,
            request.RoleId,
            cancellationToken
        );
        return new UserListPageDto(
            page.Items.Select(UserDto.FromUser).ToArray(),
            page.TotalCount,
            skip,
            take
        );
    }
}
