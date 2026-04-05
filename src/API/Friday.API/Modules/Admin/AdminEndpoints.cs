using Friday.API.Common;
using Friday.Modules.Admin.Application.Features.Rights;
using Friday.Modules.Admin.Application.Features.Roles;
using Friday.Modules.Admin.Application.Features.Users;
using Friday.Modules.Admin.Application.Models;
using LinKit.Core.Cqrs;

namespace Friday.API.Modules.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminModule(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization();

        group.MapPost(
            "/users",
            async (
                HttpContext context,
                CreateUserCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                UserDto response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapGet(
            "/users",
            async (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
            {
                IReadOnlyList<UserDto> response = await mediator.QueryAsync(
                    new GetUsersQuery(),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapGet(
            "/users/{userId:int}",
            async (
                HttpContext context,
                int userId,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                UserDto response = await mediator.QueryAsync(
                    new GetUserByIdQuery(userId),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPut(
            "/users/{userId:int}",
            async (
                HttpContext context,
                int userId,
                UpdateUserRequest body,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                UpdateUserCommand command = new(
                    userId,
                    body.UserCode,
                    body.Username,
                    body.Email,
                    body.FullName,
                    body.Phone,
                    body.Address,
                    body.CompanyName,
                    body.JobTitle,
                    body.Notes
                );
                UserDto response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/users/{userId:int}/password",
            async (
                HttpContext context,
                int userId,
                ResetUserPasswordRequest body,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                UserDto response = await mediator.SendAsync(
                    new ResetUserPasswordCommand(userId, body.NewPassword),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/users/{userId:int}/roles/{roleId:int}",
            async (
                HttpContext context,
                int userId,
                int roleId,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                var response = await mediator.SendAsync(
                    new AssignRoleToUserCommand(userId, roleId),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/users/{userId:int}/lock",
            async (
                HttpContext context,
                int userId,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                var response = await mediator.SendAsync(
                    new LockUserCommand(userId),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/roles",
            async (
                HttpContext context,
                CreateRoleCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                var response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapGet(
            "/roles",
            async (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var response = await mediator.QueryAsync(new GetRolesQuery(), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/roles/{roleId:int}/rights",
            async (
                HttpContext context,
                int roleId,
                int[] rightIds,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                var response = await mediator.SendAsync(
                    new GrantRightsToRoleCommand(roleId, rightIds),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/rights",
            async (
                HttpContext context,
                CreateRightCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                var response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapGet(
            "/rights",
            async (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var response = await mediator.QueryAsync(new GetRightsQuery(), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        return endpoints;
    }
}
