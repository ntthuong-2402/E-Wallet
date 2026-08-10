namespace Friday.Modules.Admin.Application.Configuration;

public sealed class PasswordPolicyOptions
{
    public const string SectionName = "Authentication:PasswordPolicy";
    public int MinimumLength { get; set; } = 12;
    public int MaximumLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
}
