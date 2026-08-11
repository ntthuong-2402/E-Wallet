using System.Diagnostics;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Domain.Audit;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Friday.Modules.Admin.Infrastructure.Auditing;

public sealed class EfSecurityAuditWriter(
    FridayDbContext dbContext,
    IHttpContextAccessor httpContextAccessor
) : ISecurityAuditWriter
{
    public async Task WriteAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default
    )
    {
        HttpContext? http = httpContextAccessor.HttpContext;
        int? actorUserId = record.ActorUserId;
        string? subject = http?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (actorUserId is null && int.TryParse(subject, out int parsedActorUserId))
        {
            actorUserId = parsedActorUserId;
        }
        SecurityAuditEvent auditEvent = SecurityAuditEvent.Create(
            record.EventType,
            actorUserId,
            record.Outcome,
            DateTime.UtcNow,
            record.TargetType,
            record.TargetId,
            record.ReasonCode,
            http?.Connection.RemoteIpAddress?.ToString(),
            http?.Request.Headers.UserAgent.ToString(),
            Activity.Current?.TraceId.ToString() ?? http?.TraceIdentifier,
            record.MetadataJson
        );

        await dbContext.Set<SecurityAuditEvent>().AddAsync(auditEvent, cancellationToken);
    }

    public async Task WriteImmediateAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default
    )
    {
        await WriteAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
