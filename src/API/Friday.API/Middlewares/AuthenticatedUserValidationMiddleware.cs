using System.Diagnostics;
using System.Security.Claims;
using Friday.API.Common;
using Friday.BuildingBlocks.Application.Errors;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Friday.API.Middlewares;

/// <summary>
/// After JWT signature validation, ensures the session row is still active and the user account is usable.
/// </summary>
public sealed class AuthenticatedUserValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IErrorMessageLocalizer localizer,
        IUserSessionRepository sessions,
        IUserRepository users
    )
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        string? sub =
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(sub) || !int.TryParse(sub, out int userId))
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Admin.SessionInvalid,
                "Invalid token subject.",
                localizer
            );
            return;
        }

        string? jti = context.User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrEmpty(jti) || !Guid.TryParse(jti, out Guid sessionId))
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Admin.SessionInvalid,
                "Invalid session in token.",
                localizer
            );
            return;
        }

        UserSession? session = await sessions.GetByIdAsync(sessionId, context.RequestAborted);

        DateTime now = DateTime.UtcNow;
        if (
            session is null
            || session.RevokedAtUtc is not null
            || session.ExpiresAtUtc <= now
            || session.UserId != userId
        )
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Admin.SessionInvalid,
                "Session is no longer valid.",
                localizer
            );
            return;
        }

        User? user = await users.GetByIdAsync(userId, context.RequestAborted);
        if (user is null)
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Admin.SessionInvalid,
                "User is no longer valid.",
                localizer
            );
            return;
        }

        if (user.IsLocked)
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCodes.Admin.UserLockedAuth,
                "User account is locked.",
                localizer
            );
            return;
        }

        if (!user.IsActive)
        {
            await WriteJsonAsync(
                context,
                StatusCodes.Status403Forbidden,
                ErrorCodes.Admin.UserInactive,
                "User account is inactive.",
                localizer
            );
            return;
        }

        await next(context);
    }

    private static async Task WriteJsonAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string fallbackMessage,
        IErrorMessageLocalizer localizer
    )
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        string language = context.Request.Headers.AcceptLanguage.ToString();
        string message = await localizer.GetMessageAsync(
            errorCode,
            language,
            fallbackMessage,
            context.RequestAborted
        );
        string traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(errorCode, message, traceId));
    }
}
