using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.PaymentLedger.Application.Errors;
using Friday.Modules.PaymentLedger.Domain.Ledger;

namespace Friday.Modules.PaymentLedger.Application.Features;

internal static class IdempotencySupport
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static string Hash(params object?[] parts) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts, JsonOptions)))
        );

    internal static T Replay<T>(IdempotencyRecord record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
            throw new FridayException(
                PaymentLedgerErrorCodes.RefIdConflict,
                "RefId was already used with a different request payload.",
                409
            );
        return JsonSerializer.Deserialize<T>(record.ResponseJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored PaymentLedger response is invalid.");
    }
}
