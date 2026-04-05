using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Admin.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "admin");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(512);
        builder.Property(x => x.CompanyName).HasMaxLength(256);
        builder.Property(x => x.JobTitle).HasMaxLength(128);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsLocked).IsRequired();
        builder.Property(x => x.CreatedOnUtc).IsRequired();
        builder.Property(x => x.UpdatedOnUtc).IsRequired();

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.UserCode).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.HasMany(x => x.UserRoles).WithOne().HasForeignKey(x => x.UserId);
    }
}
