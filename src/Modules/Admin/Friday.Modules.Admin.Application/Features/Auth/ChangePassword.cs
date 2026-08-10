using System.Security.Claims;
using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Admin.Domain.Security;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Friday.Modules.Admin.Application.Features.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<bool>;

public sealed class ChangePasswordHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IPasswordHasher<CredentialUser> passwordHasher,
    IPasswordPolicy passwordPolicy,
    IHttpContextAccessor httpContextAccessor,
    ISecurityAuditWriter audit
) : ICommandHandler<ChangePasswordCommand, bool>
{
    private static readonly CredentialUser CredentialMarker = new();

    public async Task<bool> HandleAsync(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        string? subject = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
        if (!int.TryParse(subject, out int userId))
        {
            throw new FridayException(ErrorCodes.Admin.SessionInvalid, "Session is invalid.", StatusCodes.Status401Unauthorized);
        }

        var user = await users.GetByIdWithPasswordAsync(userId, cancellationToken)
            ?? throw new FridayException(ErrorCodes.Admin.UserNotFound, "User was not found.", StatusCodes.Status404NotFound);
        if (user.PasswordCredential is null || passwordHasher.VerifyHashedPassword(
                CredentialMarker,
                user.PasswordCredential.PasswordHash,
                request.CurrentPassword
            ) == PasswordVerificationResult.Failed)
        {
            throw new FridayException(ErrorCodes.Admin.CurrentPasswordInvalid, "Current password is invalid.", StatusCodes.Status401Unauthorized);
        }

        passwordPolicy.Validate(request.NewPassword);
        if (passwordHasher.VerifyHashedPassword(CredentialMarker, user.PasswordCredential.PasswordHash, request.NewPassword) != PasswordVerificationResult.Failed)
        {
            throw new FridayException(ErrorCodes.Admin.PasswordPolicyViolation, "New password must differ from the current password.");
        }

        user.SetOrUpdatePasswordHash(passwordHasher.HashPassword(CredentialMarker, request.NewPassword));
        user.MarkPasswordChanged();
        await sessions.RevokeAllForUserAsync(user.Id, cancellationToken);
        await audit.WriteAsync(new("PASSWORD_CHANGED", "SUCCESS", ActorUserId: user.Id, TargetType: "USER", TargetId: user.Id.ToString()), cancellationToken);
        return true;
    }
}
