namespace Friday.Modules.Admin.Application.Configuration;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "Admin:Bootstrap";
    public string SuperAdminUserCode { get; set; } = string.Empty;
}
