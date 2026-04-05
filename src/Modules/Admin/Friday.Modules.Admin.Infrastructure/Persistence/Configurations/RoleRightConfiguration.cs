using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.Admin.Infrastructure.Persistence.Configurations;

public sealed class RoleRightConfiguration : IEntityTypeConfiguration<RoleRight>
{
    public void Configure(EntityTypeBuilder<RoleRight> builder)
    {
        builder.ToTable("role_rights", "admin");
        builder.HasKey(x => new { x.RoleId, x.RightId });
    }
}
