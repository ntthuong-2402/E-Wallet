using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Admin.Application.Security;

public sealed class PasswordPolicy(IOptions<PasswordPolicyOptions> options) : IPasswordPolicy
{
    public void Validate(string password)
    {
        PasswordPolicyOptions policy = options.Value;
        if (
            string.IsNullOrWhiteSpace(password)
            || password.Length < policy.MinimumLength
            || password.Length > policy.MaximumLength
            || (policy.RequireUppercase && !password.Any(char.IsUpper))
            || (policy.RequireLowercase && !password.Any(char.IsLower))
            || (policy.RequireDigit && !password.Any(char.IsDigit))
        )
        {
            throw new FridayException(
                ErrorCodes.Admin.PasswordPolicyViolation,
                "Password does not satisfy the configured password policy."
            );
        }
    }
}
