namespace Friday.Modules.Admin.Application.Security;

public interface IJwtTokenIssuer
{
    JwtAccessTokenResult CreateAccessToken(
        int userId,
        Guid sessionId,
        IReadOnlyList<string> roleCodes
    );
}

public sealed record JwtAccessTokenResult(string Token, DateTime ExpiresAtUtc);
