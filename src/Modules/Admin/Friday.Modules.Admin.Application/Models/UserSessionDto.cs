using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

namespace Friday.Modules.Admin.Application.Models;

public sealed record UserSessionDto(
    Guid Id,
    DateTime CreatedOnUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    string? IpAddress,
    string? UserAgent
)
{
    public static UserSessionDto FromSession(UserSession session) =>
        new(
            session.Id,
            session.CreatedOnUtc,
            session.ExpiresAtUtc,
            session.RevokedAtUtc,
            session.IpAddress,
            session.UserAgent
        );
}
