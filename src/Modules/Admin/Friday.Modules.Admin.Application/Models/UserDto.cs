using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

namespace Friday.Modules.Admin.Application.Models;

public sealed record UserDto(
    int Id,
    string UserCode,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Address,
    string? CompanyName,
    string? JobTitle,
    string? Notes,
    bool IsActive,
    bool IsLocked,
    int[] RoleIds
)
{
    public static UserDto FromUser(User user) =>
        new(
            user.Id,
            user.UserCode,
            user.Username,
            user.Email,
            user.FullName,
            user.Phone,
            user.Address,
            user.CompanyName,
            user.JobTitle,
            user.Notes,
            user.IsActive,
            user.IsLocked,
            user.UserRoles.Select(x => x.RoleId).ToArray()
        );
}
