using Microsoft.AspNetCore.Authorization;

namespace Friday.Modules.Admin.Application.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
