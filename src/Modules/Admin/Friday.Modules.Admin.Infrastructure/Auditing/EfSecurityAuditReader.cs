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
        string? eventType,
        int? actorUserId,
        string? targetType,
        string? targetId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<SecurityAuditEvent> query = dbContext.Set<SecurityAuditEvent>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            string normalized = eventType.Trim().ToUpperInvariant();
            query = query.Where(x => x.EventType == normalized);
        }
        if (actorUserId is not null)
        {
            query = query.Where(x => x.ActorUserId == actorUserId);
        }
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            string normalized = targetType.Trim().ToUpperInvariant();
            query = query.Where(x => x.TargetType == normalized);
        }
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            string normalized = targetId.Trim();
            query = query.Where(x => x.TargetId == normalized);
        }
        if (fromUtc is not null)
        {
            query = query.Where(x => x.OccurredOnUtc >= fromUtc.Value);
        }
        if (toUtc is not null)
        {
            query = query.Where(x => x.OccurredOnUtc <= toUtc.Value);
        }

        return await query
        .OrderByDescending(x => x.OccurredOnUtc)
        .Skip(skip)
        .Take(take)
        .Select(x => new SecurityAuditEventDto(x.Id, x.EventType, x.ActorUserId, x.TargetType, x.TargetId, x.Outcome, x.ReasonCode, x.IpAddress, x.TraceId, x.OccurredOnUtc))
        .ToListAsync(cancellationToken);
    }
}
