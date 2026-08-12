using Microsoft.EntityFrameworkCore;
using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Customers;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Infrastructure.Persistence;

public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options)
    : DbContext(options)
{
    public const string SchemaName = "customer";
    public const string MigrationHistoryTableName = "__EFMigrationsHistory";

    public DbSet<CustomerAggregate> Customers => Set<CustomerAggregate>();
    public DbSet<CustomerChangeAudit> CustomerChangeAudits => Set<CustomerChangeAudit>();
    public DbSet<CustomerCreateReference> CustomerCreateReferences => Set<CustomerCreateReference>();
    public DbSet<CustomerAccountLinkage> CustomerAccountLinkages => Set<CustomerAccountLinkage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuditIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
    }

    private void EnsureAuditIsAppendOnly()
    {
        bool mutation = ChangeTracker.Entries<CustomerChangeAudit>()
            .Any(x => x.State is EntityState.Modified or EntityState.Deleted);
        if (mutation)
            throw new InvalidOperationException("Customer audit records are append-only.");
    }
}
