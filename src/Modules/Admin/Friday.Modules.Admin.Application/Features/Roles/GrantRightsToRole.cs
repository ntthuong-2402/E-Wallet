using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Roles;

public sealed record GrantRightsToRoleCommand(int RoleId, int[] RightIds) : ICommand<RoleDto>;

public sealed class GrantRightsToRoleHandler(IRoleRepository roles, IRightRepository rights)
    : ICommandHandler<GrantRightsToRoleCommand, RoleDto>
{
    public async Task<RoleDto> HandleAsync(
        GrantRightsToRoleCommand request,
        CancellationToken cancellationToken
    )
    {
        Domain.Aggregates.RoleAggregate.Role? role = await roles.GetByIdAsync(
            request.RoleId,
            cancellationToken
        );
        if (role is null)
        {
            throw new FridayException(
                ErrorCodes.Admin.RoleNotFound,
                $"Role '{request.RoleId}' was not found.",
                StatusCodes.Status404NotFound
            );
        }

        int[] rightIds = request.RightIds.Distinct().ToArray();
        if (rightIds.Length > 0)
        {
            IReadOnlyList<Domain.Aggregates.RightAggregate.Right> existingRights =
                await rights.GetByIdsAsync(rightIds, cancellationToken);
            if (existingRights.Count != rightIds.Length)
            {
                throw new FridayException(
                    ErrorCodes.Admin.RightNotFound,
                    "Some rights are not found."
                );
            }
        }

        role.SetRights(rightIds);
        return new RoleDto(
            role.Id,
            role.Code,
            role.Name,
            role.IsActive,
            role.RoleRights.Select(x => x.RightId).ToArray()
        );
    }
}
