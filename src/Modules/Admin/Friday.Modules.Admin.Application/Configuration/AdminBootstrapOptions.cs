namespace Friday.Modules.Admin.Application.Configuration;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "Admin:Bootstrap";
    public bool Enabled { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string InitialPassword { get; set; } = string.Empty;
}
