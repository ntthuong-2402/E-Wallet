using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Admin.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "admin");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedOnUtc).IsRequired();
        builder.Property(x => x.UpdatedOnUtc).IsRequired();

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasMany(x => x.RoleRights).WithOne().HasForeignKey(x => x.RoleId);
    }
}
