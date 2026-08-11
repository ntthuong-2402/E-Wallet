namespace Friday.Modules.Admin.Application.Authorization;

public static class AdminPermissions
{
    public const string UsersRead = "USERS_READ";
    public const string UsersCreate = "USERS_CREATE";
    public const string UsersUpdate = "USERS_UPDATE";
    public const string UsersLock = "USERS_LOCK";
    public const string UsersUnlock = "USERS_UNLOCK";
    public const string UsersActivate = "USERS_ACTIVATE";
    public const string UsersDeactivate = "USERS_DEACTIVATE";
    public const string UsersResetPassword = "USERS_RESET_PASSWORD";
    public const string UsersRevokeSessions = "USERS_REVOKE_SESSIONS";
    public const string UsersAssignRole = "USERS_ASSIGN_ROLE";
    public const string UsersRemoveRole = "USERS_REMOVE_ROLE";
    public const string RolesRead = "ROLES_READ";
    public const string RolesManage = "ROLES_MANAGE";
    public const string RightsRead = "RIGHTS_READ";
    public const string RightsManage = "RIGHTS_MANAGE";
    public const string AuditRead = "AUDIT_READ";

    public static IReadOnlyCollection<string> All { get; } =
    [
        UsersRead, UsersCreate, UsersUpdate, UsersLock, UsersUnlock, UsersActivate,
        UsersDeactivate, UsersResetPassword, UsersRevokeSessions, UsersAssignRole,
        UsersRemoveRole,
        RolesRead, RolesManage, RightsRead, RightsManage, AuditRead,
    ];
}
