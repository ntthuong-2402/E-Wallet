namespace Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

/// <summary>1:1 credential row for <see cref="User"/>; hash is produced by application layer (e.g. ASP.NET Identity password hasher).</summary>
public sealed class UserPassword
{
    private UserPassword() { }

    public int UserId { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public User User { get; private set; } = null!;

    public static UserPassword Create(User user, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new UserPassword { User = user, PasswordHash = passwordHash };
    }

    public void UpdateHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }
}
