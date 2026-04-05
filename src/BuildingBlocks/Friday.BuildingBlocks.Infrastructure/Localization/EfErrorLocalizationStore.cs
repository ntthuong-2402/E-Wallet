using Friday.BuildingBlocks.Application.Localization;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Friday.BuildingBlocks.Infrastructure.Localization;

public sealed class EfErrorLocalizationStore(FridayDbContext dbContext) : IErrorLocalizationStore
{
    public async Task<string?> GetMessageAsync(
        string module,
        string errorCode,
        string language,
        CancellationToken cancellationToken = default
    )
    {
        return await dbContext
            .Set<ErrorLocalizationMessage>()
            .Where(x => x.Module == module && x.ErrorCode == errorCode && x.Language == language)
            .Select(x => x.Message)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
