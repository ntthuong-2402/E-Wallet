namespace Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

/// <summary>Persisted refresh-token session (access JWT references <see cref="Id"/> as <c>jti</c>).</summary>
public sealed class UserSession
{
    private UserSession() { }

    public Guid Id { get; private set; }
    public int UserId { get; private set; }

    /// <summary>Lowercase hex SHA-256 of the opaque refresh token.</summary>
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public static UserSession Create(
        int userId,
        string refreshTokenHashHex,
        DateTime expiresAtUtc,
        string? ipAddress,
        string? userAgent
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(userId, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHashHex);

        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshTokenHash = refreshTokenHashHex,
            ExpiresAtUtc = expiresAtUtc,
            CreatedOnUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };
    }

    public void Revoke() => RevokedAtUtc = DateTime.UtcNow;

    public void RotateRefresh(string newRefreshTokenHashHex, DateTime newExpiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newRefreshTokenHashHex);
        RefreshTokenHash = newRefreshTokenHashHex;
        ExpiresAtUtc = newExpiresAtUtc;
    }
}
