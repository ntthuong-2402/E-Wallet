using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.BuildingBlocks.Infrastructure.Localization;

public sealed class ErrorLocalizationMessageConfiguration
    : IEntityTypeConfiguration<ErrorLocalizationMessage>
{
    public void Configure(EntityTypeBuilder<ErrorLocalizationMessage> builder)
    {
        builder.ToTable("error_messages", "localization");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UpdatedOnUtc).IsRequired();

        builder
            .HasIndex(x => new
            {
                x.Module,
                x.ErrorCode,
                x.Language,
            })
            .IsUnique();
    }
}
