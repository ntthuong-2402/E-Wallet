using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record GetUserByIdQuery(int UserId) : IQuery<UserDto>;

public sealed class GetUserByIdHandler(IUserRepository users)
    : IQueryHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> HandleAsync(
        GetUserByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        Domain.Aggregates.UserAggregate.User? user = await users.GetByIdAsync(
            request.UserId,
            cancellationToken
        );
        if (user is null)
        {
            throw new FridayException(
                ErrorCodes.Admin.UserNotFound,
                $"User '{request.UserId}' was not found.",
                StatusCodes.Status404NotFound
            );
        }

        return UserDto.FromUser(user);
    }
}
