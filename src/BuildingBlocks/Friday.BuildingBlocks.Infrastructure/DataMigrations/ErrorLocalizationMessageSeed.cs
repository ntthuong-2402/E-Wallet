using Friday.BuildingBlocks.Application.Errors;

namespace Friday.BuildingBlocks.Infrastructure.DataMigrations;

/// <summary>
/// Reference rows for <c>localization.error_messages</c>. Replace or extend this list; future migrations can append new versions.
/// </summary>
internal static class ErrorLocalizationMessageSeed
{
    internal readonly record struct Row(
        string Module,
        string ErrorCode,
        string Language,
        string Message
    );

    internal static IReadOnlyList<Row> DefaultRows { get; } =
        [
            new("admin", ErrorCodes.Admin.UserNotFound, "en", "User was not found."),
            new("admin", ErrorCodes.Admin.UserNotFound, "vi", "Khong tim thay nguoi dung."),
            new(
                "common",
                ErrorCodes.Common.InternalServerError,
                "en",
                "An unexpected error occurred."
            ),
            new("common", ErrorCodes.Common.InternalServerError, "vi", "Da xay ra loi he thong."),
            new("common", ErrorCodes.Common.NotFound, "en", "Resource was not found."),
            new("common", ErrorCodes.Common.NotFound, "vi", "Khong tim thay du lieu."),
            new("common", ErrorCodes.Common.BadRequest, "en", "Request data is invalid."),
            new("common", ErrorCodes.Common.BadRequest, "vi", "Du lieu gui len khong hop le."),
            new("admin", ErrorCodes.Admin.UserCodeExists, "en", "User code is already in use."),
            new("admin", ErrorCodes.Admin.UserCodeExists, "vi", "Ma nguoi dung da ton tai."),
            new("admin", ErrorCodes.Admin.InvalidCredentials, "en", "Invalid login or password."),
            new("admin", ErrorCodes.Admin.InvalidCredentials, "vi", "Dang nhap hoac mat khau khong dung."),
            new("admin", ErrorCodes.Admin.UserInactive, "en", "User account is inactive."),
            new("admin", ErrorCodes.Admin.UserInactive, "vi", "Tai khoan nguoi dung khong hoat dong."),
            new("admin", ErrorCodes.Admin.UserLockedAuth, "en", "User account is locked."),
            new("admin", ErrorCodes.Admin.UserLockedAuth, "vi", "Tai khoan nguoi dung bi khoa."),
            new("admin", ErrorCodes.Admin.PasswordRequired, "en", "Password is required."),
            new("admin", ErrorCodes.Admin.PasswordRequired, "vi", "Can nhap mat khau."),
            new("admin", ErrorCodes.Admin.InvalidRefreshToken, "en", "Refresh token is invalid or expired."),
            new("admin", ErrorCodes.Admin.InvalidRefreshToken, "vi", "Refresh token khong hop le hoac het han."),
            new("admin", ErrorCodes.Admin.SessionInvalid, "en", "Session is no longer valid."),
            new("admin", ErrorCodes.Admin.SessionInvalid, "vi", "Phien lam viec khong con hop le."),
            new(
                "admin",
                ErrorCodes.Admin.RegistrationDisabled,
                "en",
                "Public registration is disabled."
            ),
            new(
                "admin",
                ErrorCodes.Admin.RegistrationDisabled,
                "vi",
                "Dang ky cong khai da tat."
            ),
        ];
}
