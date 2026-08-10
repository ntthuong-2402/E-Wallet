using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Auditing;

public sealed class EfSecurityAuditReader(FridayDbContext dbContext) : ISecurityAuditReader
{
    public async Task<IReadOnlyList<SecurityAuditEventDto>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default
    ) => await dbContext.Set<SecurityAuditEvent>()
        .AsNoTracking()
        .OrderByDescending(x => x.OccurredOnUtc)
        .Skip(skip)
        .Take(take)
        .Select(x => new SecurityAuditEventDto(x.Id, x.EventType, x.ActorUserId, x.TargetType, x.TargetId, x.Outcome, x.ReasonCode, x.IpAddress, x.TraceId, x.OccurredOnUtc))
        .ToListAsync(cancellationToken);
}
