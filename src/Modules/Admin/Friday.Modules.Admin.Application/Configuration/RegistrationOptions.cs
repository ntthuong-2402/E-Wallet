namespace Friday.Modules.Admin.Application.Configuration;

public sealed class RegistrationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>When true, <c>POST /api/auth/register</c> is allowed (dev / bootstrap).</summary>
    public bool AllowPublicRegistration { get; set; }
}
