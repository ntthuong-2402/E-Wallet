namespace Friday.Modules.Admin.Application.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Authentication:Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Friday.API";
    public string Audience { get; set; } = "Friday.Clients";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}
