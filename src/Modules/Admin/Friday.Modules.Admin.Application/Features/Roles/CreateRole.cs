using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Roles;

public sealed record CreateRoleCommand(string Code, string Name) : ICommand<RoleDto>;

public sealed class CreateRoleHandler(IRoleRepository roles)
    : ICommandHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> HandleAsync(
        CreateRoleCommand request,
        CancellationToken cancellationToken
    )
    {
        if (await roles.ExistsByCodeAsync(request.Code, cancellationToken))
        {
            throw new FridayException(ErrorCodes.Admin.RoleCodeExists, "Role code already exists.");
        }

        Role role = Role.Create(request.Code, request.Name);
        await roles.AddAsync(role, cancellationToken);

        return new RoleDto(role.Id, role.Code, role.Name, role.IsActive, []);
    }
}
