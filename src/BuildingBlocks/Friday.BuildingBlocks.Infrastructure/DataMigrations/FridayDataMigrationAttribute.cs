using System.Globalization;
using FluentMigrator;

namespace Friday.BuildingBlocks.Infrastructure.DataMigrations;

/// <summary>
/// Data migration attribute: parses a UTC timestamp string into a <see cref="long"/> version
/// (<c>yyyyMMddHHmmss</c>) and passes <paramref name="description"/> into FluentMigrator’s version row
/// <c>Description</c> column (default <c>dbo.VersionInfo</c>; see <see cref="FridayVersionTableMetaData"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FridayDataMigrationAttribute : MigrationAttribute
{
    public FridayDataMigrationAttribute(string timestampUtc, string description)
        : base(ParseVersion(timestampUtc), description)
    {
        TimestampUtc = timestampUtc;
    }

    public FridayDataMigrationAttribute(
        string timestampUtc,
        string description,
        TransactionBehavior transactionBehavior
    )
        : base(ParseVersion(timestampUtc), transactionBehavior, description)
    {
        TimestampUtc = timestampUtc;
    }

    /// <summary>Original timestamp argument (for tooling/diagnostics).</summary>
    public string TimestampUtc { get; }

    /// <summary>
    /// Parses <paramref name="timestampUtc"/> (ISO 8601 or invariant date/time), normalizes to UTC,
    /// then formats as <c>yyyyMMddHHmmss</c> and parses to <see cref="long"/>.
    /// </summary>
    public static long ParseVersion(string timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(timestampUtc))
        {
            throw new ArgumentException("Timestamp is required.", nameof(timestampUtc));
        }

        if (
            !DateTime.TryParse(
                timestampUtc.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime utc
            )
        )
        {
            throw new ArgumentException(
                $"Could not parse migration timestamp: {timestampUtc}",
                nameof(timestampUtc)
            );
        }

        string compact = utc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return long.Parse(compact, CultureInfo.InvariantCulture);
    }
}
