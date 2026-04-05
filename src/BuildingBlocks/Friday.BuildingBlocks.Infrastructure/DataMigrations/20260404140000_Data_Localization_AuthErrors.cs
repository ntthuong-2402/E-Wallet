using FluentMigrator;
using Friday.BuildingBlocks.Infrastructure.Hosting;
using Friday.BuildingBlocks.Infrastructure.Localization;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.BuildingBlocks.Infrastructure.DataMigrations;

[FridayDataMigration(
    "2026-04-04T14:00:00Z",
    "Data: localization.error_messages — auth-related admin error codes."
)]
public sealed class Data_20260404140000_Localization_AuthErrors : AutoReversingMigration
{
    public override void Up()
    {
        ApplicationServiceProviderAccessor
            .ExecuteInNewScopeAsync(ApplySeedAsync, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ApplySeedAsync(
        IServiceProvider sp,
        CancellationToken cancellationToken
    )
    {
        DateTime now = DateTime.UtcNow;
        FridayDbContext db = sp.GetRequiredService<FridayDbContext>();

        foreach (ErrorLocalizationMessageSeed.Row row in ErrorLocalizationMessageSeed.DefaultRows)
        {
            ErrorLocalizationMessage? existing = await db.Set<ErrorLocalizationMessage>()
                .FirstOrDefaultAsync(
                    x =>
                        x.Module == row.Module
                        && x.ErrorCode == row.ErrorCode
                        && x.Language == row.Language,
                    cancellationToken
                );

            if (existing is null)
            {
                db.Add(
                    new ErrorLocalizationMessage
                    {
                        Module = row.Module,
                        ErrorCode = row.ErrorCode,
                        Language = row.Language,
                        Message = row.Message,
                        UpdatedOnUtc = now,
                    }
                );
            }
            else if (!string.Equals(existing.Message, row.Message, StringComparison.Ordinal))
            {
                existing.Message = row.Message;
                existing.UpdatedOnUtc = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
