using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Rights;

public sealed record GetRightsQuery() : IQuery<IReadOnlyList<RightDto>>;

public sealed class GetRightsHandler(IRightRepository rights)
    : IQueryHandler<GetRightsQuery, IReadOnlyList<RightDto>>
{
    public async Task<IReadOnlyList<RightDto>> HandleAsync(
        GetRightsQuery request,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<Domain.Aggregates.RightAggregate.Right> items = await rights.ListAsync(
            cancellationToken
        );
        return items.Select(x => new RightDto(x.Id, x.Code, x.Name, x.Description)).ToArray();
    }
}
