using API.Mapping;
using Domain.Games;
using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Controller;

namespace API.Endpoints;

internal static class ControllerEndpoints
{
    public static IEndpointRouteBuilder MapControllerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/controller").WithTags("Controller");

        group.MapPost("/join", Join)
            .WithName("ControllerJoin")
            .WithSummary("Join a game from an Oplà controller")
            .WithDescription("Known deviceIds get their existing player back with reconnected=true.")
            .Produces<ControllerJoinResponse>(StatusCodes.Status200OK)
            .Produces<ControllerJoinResponse>(StatusCodes.Status400BadRequest)
            .Produces<ControllerJoinResponse>(StatusCodes.Status404NotFound)
            .Produces<ControllerJoinResponse>(StatusCodes.Status409Conflict);

        return app;
    }

    private static JsonHttpResult<ControllerJoinResponse> Join(
        ControllerJoinRequest request, GameRegistry registry
    )
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Reply(StatusCodes.Status400BadRequest, ControllerJoinResponse.Fail("PIN required"));

        var game = registry.Find(request.Pin);
        if (game is null)
            return Reply(StatusCodes.Status404NotFound, ControllerJoinResponse.Fail("Unknown PIN"));

        var result = game.Join(request.Name, request.DeviceId);
        if (!result.Success)
        {
            var (status, message) = result.Error!.Value.ToHttp();
            return Reply(status, ControllerJoinResponse.Fail(message));
        }

        var player = result.Player!;
        return Reply(StatusCodes.Status200OK,
            ControllerJoinResponse.Joined(player.Id, player.Name, player.Number, result.Reconnected));
    }

    private static JsonHttpResult<ControllerJoinResponse> Reply(int status, ControllerJoinResponse body) => 
        TypedResults.Json(body, statusCode: status);
}