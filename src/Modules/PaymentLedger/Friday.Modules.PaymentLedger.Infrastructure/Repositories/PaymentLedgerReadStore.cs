using System.Text.Json;
using Friday.Modules.PaymentLedger.Application.Models;
using Friday.Modules.PaymentLedger.Application.Queries;
using Friday.Modules.PaymentLedger.Domain.Ledger;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.PaymentLedger.Infrastructure.Repositories;

public sealed class PaymentLedgerReadStore(PaymentLedgerDbContext db) : IPaymentLedgerReadStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<LedgerAccountDto?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) { LedgerAccount? value = await db.LedgerAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken); return value is null ? null : LedgerAccountDto.From(value); }
    public async Task<TransactionDto?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default) { FinancialTransaction? value = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken); return value is null ? null : TransactionDto.From(value); }
    public async Task<TransactionDto?> GetTransactionByReferenceAsync(string actor, string operation, string refId, CancellationToken cancellationToken = default)
    {
        string? json = await db.IdempotencyRecords.AsNoTracking().Where(x => x.ActorUserId == actor && x.Operation == operation && x.RefId == refId).Select(x => x.ResponseJson).SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<TransactionDto>(json, JsonOptions);
    }
    public async Task<TransactionSearchPage> SearchAsync(TransactionSearchCriteria c, CancellationToken cancellationToken = default)
    {
        IQueryable<FinancialTransaction> query = Query();
        if (c.AccountId.HasValue) query = query.Where(x => x.SourceAccountId == c.AccountId || x.DestinationAccountId == c.AccountId);
        if (c.Currency is not null) query = query.Where(x => x.Currency == c.Currency);
        if (c.Type.HasValue) query = query.Where(x => x.Type == c.Type); if (c.Status.HasValue) query = query.Where(x => x.Status == c.Status);
        if (c.PostedFromUtc.HasValue) query = query.Where(x => x.PostedOnUtc >= c.PostedFromUtc); if (c.PostedToUtc.HasValue) query = query.Where(x => x.PostedOnUtc <= c.PostedToUtc);
        int count = await query.CountAsync(cancellationToken); List<FinancialTransaction> values = await query.OrderByDescending(x => x.PostedOnUtc).ThenByDescending(x => x.Id).Skip((c.Page - 1) * c.PageSize).Take(c.PageSize).ToListAsync(cancellationToken);
        return new(values.Select(TransactionDto.From).ToArray(), count, c.Page, c.PageSize);
    }
    private IQueryable<FinancialTransaction> Query() => db.FinancialTransactions.AsNoTracking().Include(x => x.Journal).ThenInclude(x => x.Entries).AsSplitQuery();
}
