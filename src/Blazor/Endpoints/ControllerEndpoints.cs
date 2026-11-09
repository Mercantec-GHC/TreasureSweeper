using API.Mapping;
using Domain.Boards;
using Domain.Games;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Shared.Controller;
using System.Diagnostics.CodeAnalysis;

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

        group.MapPost("/heartbeat", Heartbeat)
            .WithName("ControllerHeartbeat")
            .WithSummary("Keep a controller session alive")
            .Produces<ControllerJoinResponse>(StatusCodes.Status200OK)
            .Produces<ControllerJoinResponse>(StatusCodes.Status403Forbidden)
            .Produces<ControllerJoinResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/action", Action)
            .WithName("ControllerAction")
            .WithSummary("Send move, rotate, scan or dig")
            .WithDescription("move = one step forward; rotate need params.direction LEFT/RIGHT. Rule rejections return 200 with ok=false")
            .Produces<ControllerJoinResponse>(StatusCodes.Status200OK)
            .Produces<ControllerJoinResponse>(StatusCodes.Status400BadRequest)
            .Produces<ControllerJoinResponse>(StatusCodes.Status403Forbidden)
            .Produces<ControllerJoinResponse>(StatusCodes.Status404NotFound);

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
            ControllerJoinResponse.Joined(player.Id, player.Name, player.Number, player.Color, result.Reconnected));
    }

    private static JsonHttpResult<ControllerJoinResponse> Heartbeat(
        ControllerHeartbeatRequest request, GameRegistry registry
    )
    {
        if (!TryResolve(registry, request.Pin, request.PlayerId, request.DeviceId, out _, out _, out var error))
            return error;

        return Reply(StatusCodes.Status200OK, ControllerJoinResponse.Success());
    }

    private static JsonHttpResult<ControllerJoinResponse> Action(
        ControllerActionRequest request, GameRegistry registry
    )
    {
        if (!TryResolve(registry, request.Pin, request.PlayerId, request.DeviceId, out var game, out var player, out var error))
            return error;
        
        var action = PlayerAction.Parse(request.Action, request.Params?.Direction);
        if (action is null)
            return Reply(StatusCodes.Status400BadRequest, ControllerJoinResponse.Fail("Unknown action or direction"));

        var result = game.Act(player, action);
        if (!result.Success)
            return Reply(StatusCodes.Status200OK, ControllerJoinResponse.Rejected(result.Error!.Value.ToHttp().Message));

        return Reply(StatusCodes.Status200OK, result switch
        {
            { Scan: { } scan } => ControllerJoinResponse.Scanned(scan.OnMine, scan.AdjacentMines),
            { Dig: { } dig } => ControllerJoinResponse.Dug(DigLight(dig)),
            { Mine: { } mine } => ControllerJoinResponse.Dug(MineLight(mine.Mine)),
            _ => ControllerJoinResponse.Success() 
        });
    }

    private static string DigLight(DigResult dig) => dig switch
    {
        { Found: TileContent.Treasure } => ControllerLight.Yellow,
        { Triggered: true } => MineLight(dig.Found),
        _ => ControllerLight.Off,
    };

    private static string MineLight(TileContent mine) =>
        mine == TileContent.PointMine ? ControllerLight.Purple : ControllerLight.Blue;

    private static bool TryResolve(
        GameRegistry registry, string? pin, string? playerId, string? deviceId,
        [NotNullWhen(true)] out Game? game,
        [NotNullWhen(true)] out Player? player,
        [NotNullWhen(false)] out JsonHttpResult<ControllerJoinResponse>? error
    )
    {
        player = null;
        error = null;

        game = string.IsNullOrWhiteSpace(pin) ? null : registry.Find(pin);
        if (game is null)
        {
            error = Reply(StatusCodes.Status404NotFound, ControllerJoinResponse.Fail("Unknown PIN"));
            return false;
        }

        player = game.FindController(playerId, deviceId);
        if (player is null)
        {
            error = Reply(StatusCodes.Status403Forbidden, ControllerJoinResponse.Fail("Unknown controller"));
            return false;
        }

        return true;
    }

    private static JsonHttpResult<ControllerJoinResponse> Reply(int status, ControllerJoinResponse body) => 
        TypedResults.Json(body, statusCode: status);
}