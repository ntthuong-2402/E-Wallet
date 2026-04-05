namespace Friday.Modules.Admin.Application.Models;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    UserDto User
);

public sealed record RefreshTokenResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken
);
