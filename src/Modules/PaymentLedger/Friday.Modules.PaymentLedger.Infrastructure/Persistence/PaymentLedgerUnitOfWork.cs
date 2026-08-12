using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.PaymentLedger.Application.Errors;
using Friday.Modules.PaymentLedger.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Friday.Modules.PaymentLedger.Infrastructure.Persistence;

internal sealed class PaymentLedgerUnitOfWork(PaymentLedgerDbContext dbContext) : IPaymentLedgerUnitOfWork
{
    private IDbContextTransaction? transaction;
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default) { if (transaction is null && dbContext.Database.CurrentTransaction is null && dbContext.Database.IsRelational()) transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken); }
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new FridayException(PaymentLedgerErrorCodes.ConcurrencyConflict, "Ledger state changed concurrently; retry with the same RefId.", 409); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            string constraint = pg.ConstraintName ?? string.Empty;
            if (constraint.Contains("ActorUserId", StringComparison.Ordinal) || constraint.Contains("RefId", StringComparison.Ordinal)) throw new FridayException(PaymentLedgerErrorCodes.RefIdConflict, "RefId already exists.", 409);
            if (constraint.Contains("AccountRef", StringComparison.Ordinal)) throw new FridayException(PaymentLedgerErrorCodes.AccountRefConflict, "Ledger AccountRef already exists.", 409);
            if (constraint.Contains("OriginalTransactionId", StringComparison.Ordinal)) throw new FridayException(PaymentLedgerErrorCodes.InvalidReversal, "Transaction was already reversed.", 409);
            throw;
        }
        if (transaction is not null) { await transaction.CommitAsync(cancellationToken); await transaction.DisposeAsync(); transaction = null; }
    }
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is not null)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // PostgreSQL deferred constraints can fail while COMMIT completes
                // the transaction. Preserve the original commit exception.
            }
            finally
            {
                await transaction.DisposeAsync();
                transaction = null;
            }
        }
        dbContext.ChangeTracker.Clear();
    }
}
