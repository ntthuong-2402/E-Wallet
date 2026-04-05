using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Roles;

public sealed record GetRolesQuery() : IQuery<IReadOnlyList<RoleDto>>;

public sealed class GetRolesHandler(IRoleRepository roles)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> HandleAsync(
        GetRolesQuery request,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<Domain.Aggregates.RoleAggregate.Role> items = await roles.ListAsync(
            cancellationToken
        );
        return items
            .Select(x => new RoleDto(
                x.Id,
                x.Code,
                x.Name,
                x.IsActive,
                x.RoleRights.Select(rr => rr.RightId).ToArray()
            ))
            .ToArray();
    }
}
