using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Admin.Infrastructure.Persistence.Configurations;

public sealed class UserPasswordConfiguration : IEntityTypeConfiguration<UserPassword>
{
    public void Configure(EntityTypeBuilder<UserPassword> builder)
    {
        builder.ToTable("user_passwords", "admin");
        builder.HasKey(x => x.UserId);

        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();

        builder
            .HasOne(x => x.User)
            .WithOne(x => x.PasswordCredential)
            .HasForeignKey<UserPassword>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
