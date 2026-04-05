using System.Security.Cryptography;
using System.Text;

namespace Friday.Modules.Admin.Application.Security;

public static class RefreshTokenUtilities
{
    public static string GenerateOpaqueToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string refreshToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
