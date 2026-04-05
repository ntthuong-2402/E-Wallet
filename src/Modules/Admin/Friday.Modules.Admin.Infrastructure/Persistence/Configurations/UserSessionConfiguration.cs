using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Admin.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions", "admin");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RefreshTokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        builder.HasIndex(x => x.RefreshTokenHash);
        builder.HasIndex(x => x.UserId);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
