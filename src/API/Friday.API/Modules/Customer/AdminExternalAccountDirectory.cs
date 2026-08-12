using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Customer.Application.Customers;

namespace Friday.API.Modules.Customer;

public sealed class AdminExternalAccountDirectory(IUserRepository users)
    : IExternalAccountDirectory
{
    public async Task<bool> ExistsAsync(
        string accountId,
        CancellationToken cancellationToken = default
    ) => int.TryParse(accountId, out int userId)
        && await users.GetByIdAsync(userId, cancellationToken) is not null;
}
