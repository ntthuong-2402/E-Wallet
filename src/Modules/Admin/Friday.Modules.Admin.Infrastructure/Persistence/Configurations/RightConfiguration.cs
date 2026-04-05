using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Admin.Infrastructure.Persistence.Configurations;

public sealed class RightConfiguration : IEntityTypeConfiguration<Right>
{
    public void Configure(EntityTypeBuilder<Right> builder)
    {
        builder.ToTable("rights", "admin");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedOnUtc).IsRequired();
        builder.Property(x => x.UpdatedOnUtc).IsRequired();

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
