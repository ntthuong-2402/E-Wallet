using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Rights;

public sealed record CreateRightCommand(string Code, string Name, string? Description)
    : ICommand<RightDto>;

public sealed class CreateRightHandler(IRightRepository rights, ISecurityAuditWriter audit)
    : ICommandHandler<CreateRightCommand, RightDto>
{
    public async Task<RightDto> HandleAsync(
        CreateRightCommand request,
        CancellationToken cancellationToken
    )
    {
        if (await rights.ExistsByCodeAsync(request.Code, cancellationToken))
        {
            throw new FridayException(
                ErrorCodes.Admin.RightCodeExists,
                "Right code already exists."
            );
        }

        Right right = Right.Create(request.Code, request.Name, request.Description);
        await rights.AddAsync(right, cancellationToken);
        await audit.WriteAsync(new("RIGHT_CREATED", "SUCCESS", TargetType: "RIGHT", TargetId: right.Code), cancellationToken);
        return new RightDto(right.Id, right.Code, right.Name, right.Description);
    }
}
