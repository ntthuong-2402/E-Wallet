using Friday.API.Common;
using Friday.Modules.Admin.Application.Features.Rights;
using Friday.Modules.Admin.Application.Features.Roles;
using Friday.Modules.Admin.Application.Features.Users;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Application.Features.Audit;
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
        ).RequireAuthorization(AdminPermissions.UsersCreate);

        group.MapGet(
            "/users",
            async (
                HttpContext context,
                int? skip,
                int? take,
                string? search,
                bool? isActive,
                bool? isLocked,
                int? roleId,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                UserListPageDto response = await mediator.QueryAsync(
                    new GetUsersQuery(
                        skip ?? 0,
                        take ?? 50,
                        search,
                        isActive,
                        isLocked,
                        roleId
                    ),
                    cancellationToken
                );
                context.Response.Headers["X-Total-Count"] = response.TotalCount.ToString();
                context.Response.Headers["X-Skip"] = response.Skip.ToString();
                context.Response.Headers["X-Take"] = response.Take.ToString();
                return ApiResults.Ok(context, response.Items);
            }
        ).RequireAuthorization(AdminPermissions.UsersRead);

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
        ).RequireAuthorization(AdminPermissions.UsersRead);

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
        ).RequireAuthorization(AdminPermissions.UsersUpdate);

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
        ).RequireAuthorization(AdminPermissions.UsersResetPassword);

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
        ).RequireAuthorization(AdminPermissions.UsersAssignRole);

        group.MapDelete(
            "/users/{userId:int}/roles/{roleId:int}",
            async (
                HttpContext context,
                int userId,
                int roleId,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                UserDto response = await mediator.SendAsync(
                    new RemoveRoleFromUserCommand(userId, roleId),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.UsersRemoveRole);

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
        ).RequireAuthorization(AdminPermissions.UsersLock);

        group.MapPost(
            "/users/{userId:int}/unlock",
            async (HttpContext context, int userId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                UserDto response = await mediator.SendAsync(new UnlockUserCommand(userId), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.UsersUnlock);

        group.MapPost(
            "/users/{userId:int}/activate",
            async (HttpContext context, int userId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                UserDto response = await mediator.SendAsync(new SetUserActivationCommand(userId, true), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.UsersActivate);

        group.MapPost(
            "/users/{userId:int}/deactivate",
            async (HttpContext context, int userId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                UserDto response = await mediator.SendAsync(new SetUserActivationCommand(userId, false), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.UsersDeactivate);

        group.MapGet(
            "/users/{userId:int}/sessions",
            async (HttpContext context, int userId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                IReadOnlyList<UserSessionDto> response = await mediator.QueryAsync(new GetUserSessionsQuery(userId), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.UsersRead);

        group.MapPost(
            "/users/{userId:int}/sessions/revoke",
            async (HttpContext context, int userId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                bool response = await mediator.SendAsync(new RevokeUserSessionsCommand(userId), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.UsersRevokeSessions);

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
        ).RequireAuthorization(AdminPermissions.RolesManage);

        group.MapGet(
            "/roles",
            async (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var response = await mediator.QueryAsync(new GetRolesQuery(), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.RolesRead);

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
        ).RequireAuthorization(AdminPermissions.RolesManage);

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
        ).RequireAuthorization(AdminPermissions.RightsManage);

        group.MapGet(
            "/rights",
            async (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var response = await mediator.QueryAsync(new GetRightsQuery(), cancellationToken);
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.RightsRead);

        group.MapGet(
            "/audit-events",
            async (
                HttpContext context,
                int? skip,
                int? take,
                string? eventType,
                int? actorUserId,
                string? targetType,
                string? targetId,
                DateTime? fromUtc,
                DateTime? toUtc,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                IReadOnlyList<SecurityAuditEventDto> response = await mediator.QueryAsync(
                    new GetSecurityAuditEventsQuery(
                        skip ?? 0,
                        take ?? 50,
                        eventType,
                        actorUserId,
                        targetType,
                        targetId,
                        fromUtc,
                        toUtc
                    ),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        ).RequireAuthorization(AdminPermissions.AuditRead);

        return endpoints;
    }
}
