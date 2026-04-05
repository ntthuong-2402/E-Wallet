using Friday.API.Common;
using Friday.Modules.Sample.Application.Features;
using LinKit.Core.Cqrs;

namespace Friday.API.Modules.Sample;

public static class SampleEndpoints
{
    public static IEndpointRouteBuilder MapSampleModule(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/sample").WithTags("Sample");

        group.MapPost(
            "/todos",
            async (
                HttpContext context,
                CreateTodoItemCommand command,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                var response = await mediator.SendAsync(command, cancellationToken);
                return ApiResults.Ok(context, response);
            }
        );

        group.MapGet(
            "/todos",
            async (HttpContext context, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var response = await mediator.QueryAsync(
                    new GetTodoItemsQuery(),
                    cancellationToken
                );
                return ApiResults.Ok(context, response);
            }
        );

        return endpoints;
    }
}
