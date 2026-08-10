namespace Friday.Modules.Admin.Application.Configuration;

public sealed class LoginSecurityOptions
{
    public const string SectionName = "Authentication:LoginSecurity";
    public int MaximumFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
