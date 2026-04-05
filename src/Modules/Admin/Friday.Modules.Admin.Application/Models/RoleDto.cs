namespace Friday.Modules.Admin.Application.Models;

public sealed record RoleDto(int Id, string Code, string Name, bool IsActive, int[] RightIds);
