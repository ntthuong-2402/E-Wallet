using Friday.API.Common;
using Friday.Modules.Admin.Application.Features.Auth;
using Friday.Modules.Admin.Application.Models;
using LinKit.Core.Cqrs;

namespace Friday.API.Modules.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthModule(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost(
            "/register",
            async (
                HttpContext context,
                RegisterCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                LoginResponseDto response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/login",
            async (
                HttpContext context,
                LoginCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                LoginResponseDto response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/refresh",
            async (
                HttpContext context,
                RefreshTokenCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                RefreshTokenResponseDto response = await mediator.SendAsync(
                    command,
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        group.MapPost(
            "/logout",
            async (
                HttpContext context,
                LogoutCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                bool ok = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, ok);
            }
        );

        return endpoints;
    }
}
