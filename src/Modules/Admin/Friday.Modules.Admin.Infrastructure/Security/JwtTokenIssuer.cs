using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Friday.Modules.Admin.Infrastructure.Security;

public sealed class JwtTokenIssuer(IOptions<JwtSettings> options) : IJwtTokenIssuer
{
    private readonly JwtSettings _options = options.Value;

    public JwtAccessTokenResult CreateAccessToken(
        int userId,
        Guid sessionId,
        IReadOnlyList<string> roleCodes
    )
    {
        if (string.IsNullOrWhiteSpace(_options.Secret) || _options.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:Secret must be configured and at least 32 characters."
            );
        }

        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(
            Math.Clamp(_options.AccessTokenMinutes, 1, 24 * 60)
        );

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, sessionId.ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        };

        foreach (string role in roleCodes)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_options.Secret));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds
        );

        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtAccessTokenResult(serialized, expiresAtUtc);
    }
}
