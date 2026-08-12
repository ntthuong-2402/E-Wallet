using Friday.Modules.Customer.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Customer.Infrastructure.Persistence.Configurations;

public sealed class CustomerCreateReferenceConfiguration
    : IEntityTypeConfiguration<CustomerCreateReference>
{
    public void Configure(EntityTypeBuilder<CustomerCreateReference> builder)
    {
        builder.ToTable("customer_create_references", CustomerDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint(
                "CK_customer_create_references_ref_id",
                "\"RefId\" ~ '^[A-Za-z0-9._:-]{1,100}$'"
            );
            table.HasCheckConstraint(
                "CK_customer_create_references_request_hash",
                "\"RequestHash\" ~ '^[A-F0-9]{64}$'"
            );
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.RefId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.RefId).IsUnique();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
