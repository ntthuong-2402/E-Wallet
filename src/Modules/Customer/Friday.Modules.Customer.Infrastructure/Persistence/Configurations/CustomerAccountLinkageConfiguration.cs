using Friday.Modules.Customer.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Customer.Infrastructure.Persistence.Configurations;

public sealed class CustomerAccountLinkageConfiguration
    : IEntityTypeConfiguration<CustomerAccountLinkage>
{
    public void Configure(EntityTypeBuilder<CustomerAccountLinkage> builder)
    {
        builder.ToTable("customer_account_linkages", CustomerDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint(
                "CK_customer_account_linkages_account_id",
                "char_length(\"AccountId\") BETWEEN 1 AND 128 AND btrim(\"AccountId\") = \"AccountId\""
            );
            table.HasCheckConstraint(
                "CK_customer_account_linkages_link_reason",
                "char_length(btrim(\"LinkReason\")) BETWEEN 1 AND 500"
            );
            table.HasCheckConstraint(
                "CK_customer_account_linkages_unlink_state",
                "(\"UnlinkedOnUtc\" IS NULL AND \"UnlinkedByActorUserId\" IS NULL AND \"UnlinkReason\" IS NULL) OR " +
                "(\"UnlinkedOnUtc\" IS NOT NULL AND \"UnlinkedOnUtc\" >= \"LinkedOnUtc\" AND " +
                "char_length(btrim(\"UnlinkedByActorUserId\")) BETWEEN 1 AND 128 AND " +
                "char_length(btrim(\"UnlinkReason\")) BETWEEN 1 AND 500)"
            );
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.AccountId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LinkedByActorUserId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LinkedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.LinkReason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UnlinkedByActorUserId).HasMaxLength(128);
        builder.Property(x => x.UnlinkedOnUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UnlinkReason).HasMaxLength(500);
        builder.Ignore(x => x.IsActive);
        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("\"UnlinkedOnUtc\" IS NULL");
        builder.HasIndex(x => x.AccountId)
            .IsUnique()
            .HasFilter("\"UnlinkedOnUtc\" IS NULL");
        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
