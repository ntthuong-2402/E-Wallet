using Friday.BuildingBlocks.Domain.Entities;
using Friday.Modules.Admin.Domain.Events;

namespace Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

public sealed class User : AggregateRoot
{
    private readonly List<UserRole> _userRoles = [];

    private User() { }

    public string UserCode { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string? CompanyName { get; private set; }
    public string? JobTitle { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }

    public UserPassword? PasswordCredential { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public static User Create(
        string userCode,
        string username,
        string email,
        string fullName,
        string? phone,
        string? address,
        string? companyName,
        string? jobTitle,
        string? notes
    )
    {
        ValidateUserCode(userCode);
        ValidateUsername(username);
        ValidateEmail(email);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name is required.", nameof(fullName));
        }

        User user = new()
        {
            UserCode = userCode.Trim().ToUpperInvariant(),
            Username = username.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsActive = true,
            IsLocked = false,
        };

        user.Raise(new UserCreatedDomainEvent(user.Id, user.Username, user.Email));
        return user;
    }

    public void SetPasswordCredential(UserPassword passwordCredential)
    {
        ArgumentNullException.ThrowIfNull(passwordCredential);
        PasswordCredential = passwordCredential;
    }

    public void SetOrUpdatePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (PasswordCredential is null)
        {
            SetPasswordCredential(UserPassword.Create(this, passwordHash));
        }
        else
        {
            PasswordCredential.UpdateHash(passwordHash);
        }
    }

    public void UpdateProfile(
        string fullName,
        string? phone,
        string? address,
        string? companyName,
        string? jobTitle,
        string? notes
    )
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name is required.", nameof(fullName));
        }

        FullName = fullName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Touch();
    }

    public void ChangeEmail(string email)
    {
        ValidateEmail(email);
        Email = email.Trim().ToLowerInvariant();
        Touch();
    }

    public void ChangeUserCode(string userCode)
    {
        ValidateUserCode(userCode);
        UserCode = userCode.Trim().ToUpperInvariant();
        Touch();
    }

    public void ChangeUsername(string username)
    {
        ValidateUsername(username);
        Username = username.Trim();
        Touch();
    }

    public void AssignRole(int roleId)
    {
        if (roleId <= 0)
        {
            throw new ArgumentException("RoleId must be greater than zero.", nameof(roleId));
        }

        if (_userRoles.Any(x => x.RoleId == roleId))
        {
            return;
        }

        _userRoles.Add(UserRole.Create(Id, roleId));
        Touch();
        Raise(new UserRoleAssignedDomainEvent(Id, roleId));
    }

    public void Lock()
    {
        if (IsLocked)
        {
            return;
        }

        IsLocked = true;
        Touch();
        Raise(new UserLockedDomainEvent(Id));
    }

    public void Unlock()
    {
        if (!IsLocked)
        {
            return;
        }

        IsLocked = false;
        Touch();
    }

    private static void ValidateUserCode(string userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
        {
            throw new ArgumentException("User code is required.", nameof(userCode));
        }
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }
    }
}
