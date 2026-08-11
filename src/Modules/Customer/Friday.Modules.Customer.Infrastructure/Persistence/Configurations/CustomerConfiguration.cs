using Friday.Modules.Customer.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerAggregate>
{
    public void Configure(EntityTypeBuilder<CustomerAggregate> builder)
    {
        builder.ToTable("customers", CustomerDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint("CK_customers_status", "\"Status\" IN ('Active', 'Suspended', 'Closed')");
            table.HasCheckConstraint("CK_customers_document_type", "\"CitizenDocumentType\" IN ('VietnamCitizenId', 'Passport')");
            table.HasCheckConstraint("CK_customers_country", "\"CitizenIssuingCountryCode\" ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint(
                "CK_customers_document_format",
                "(\"CitizenDocumentType\" = 'VietnamCitizenId' AND \"CitizenIssuingCountryCode\" = 'VN' AND \"CitizenDocumentNumber\" ~ '^[0-9]{12}$') OR " +
                "(\"CitizenDocumentType\" = 'Passport' AND \"CitizenDocumentNumber\" ~ '^[A-Z0-9]{6,20}$')"
            );
            table.HasCheckConstraint(
                "CK_customers_document_mask",
                "\"CitizenIdMasked\" = repeat('*', greatest(char_length(\"CitizenDocumentNumber\") - 4, 0)) || right(\"CitizenDocumentNumber\", least(4, char_length(\"CitizenDocumentNumber\")))"
            );
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CustomerCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.CustomerCode).IsUnique();
        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.DateOfBirth).HasColumnType("date");
        builder.Property(x => x.CitizenDocumentType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.CitizenIssuingCountryCode).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(x => x.CitizenDocumentNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CitizenIdMasked).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new
        {
            x.CitizenDocumentType,
            x.CitizenIssuingCountryCode,
            x.CitizenDocumentNumber,
        }).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.OpenedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.OpenedOnUtc, x.Id });
        builder.HasIndex(x => new { x.Status, x.OpenedOnUtc, x.Id });
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0L).IsRequired();
        builder.Property(x => x.CreatedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Ignore(x => x.DomainEvents);
    }
}
