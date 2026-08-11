using Friday.Modules.Customer.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Customer.Infrastructure.Persistence.Configurations;

public sealed class CustomerChangeAuditConfiguration : IEntityTypeConfiguration<CustomerChangeAudit>
{
    public void Configure(EntityTypeBuilder<CustomerChangeAudit> builder)
    {
        builder.ToTable("customer_change_audits", CustomerDbContext.SchemaName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CustomerCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ChangedFieldsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(16);
        builder.Property(x => x.ToStatus).HasMaxLength(16);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OccurredOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.RetainUntilUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CustomerId, x.OccurredOnUtc });
        builder.HasIndex(x => x.RetainUntilUtc);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
