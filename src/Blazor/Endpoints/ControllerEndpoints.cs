using API.Mapping;
using Domain.Boards;
using Domain.Games;
using Microsoft.AspNetCore.Http.HttpResults;
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
            .Produces<ControllerResponse>(StatusCodes.Status200OK)
            .Produces<ControllerResponse>(StatusCodes.Status400BadRequest)
            .Produces<ControllerResponse>(StatusCodes.Status404NotFound)
            .Produces<ControllerResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/heartbeat", Heartbeat)
            .WithName("ControllerHeartbeat")
            .WithSummary("Keep a controller session alive")
            .Produces<ControllerResponse>(StatusCodes.Status200OK)
            .Produces<ControllerResponse>(StatusCodes.Status403Forbidden)
            .Produces<ControllerResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/action", Action)
            .WithName("ControllerAction")
            .WithSummary("Send move, rotate, scan or dig")
            .WithDescription("move = one step forward; rotate need params.direction LEFT/RIGHT. Rule rejections return 200 with ok=false")
            .Produces<ControllerResponse>(StatusCodes.Status200OK)
            .Produces<ControllerResponse>(StatusCodes.Status400BadRequest)
            .Produces<ControllerResponse>(StatusCodes.Status403Forbidden)
            .Produces<ControllerResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    private static JsonHttpResult<ControllerResponse> Join(
        ControllerJoinRequest request, GameRegistry registry
    )
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
            return Reply(StatusCodes.Status400BadRequest, ControllerResponse.Fail("PIN required"));

        var game = registry.Find(request.Pin);
        if (game is null)
            return Reply(StatusCodes.Status404NotFound, ControllerResponse.Fail("Unknown PIN"));

        var result = game.Join(request.Name, request.DeviceId);
        if (!result.Success)
        {
            var (status, message) = result.Error!.Value.ToHttp();
            return Reply(status, ControllerResponse.Fail(message));
        }

        var player = result.Player!;
        return Reply(StatusCodes.Status200OK,
            ControllerResponse.Joined(player.Id, player.Name, player.Number, player.Color, result.Reconnected));
    }

    private static JsonHttpResult<ControllerResponse> Heartbeat(
        ControllerHeartbeatRequest request, GameRegistry registry
    )
    {
        if (!TryResolve(registry, request.Pin, request.PlayerId, request.DeviceId, out _, out _, out var error))
            return error;

        return Reply(StatusCodes.Status200OK, ControllerResponse.Success());
    }

    private static JsonHttpResult<ControllerResponse> Action(
        ControllerActionRequest request, GameRegistry registry
    )
    {
        if (!TryResolve(registry, request.Pin, request.PlayerId, request.DeviceId, out var game, out var player, out var error))
            return error;
        
        var action = PlayerAction.Parse(request.Action, request.Params?.Direction);
        if (action is null)
            return Reply(StatusCodes.Status400BadRequest, ControllerResponse.Fail("Unknown action or direction"));

        var result = game.Act(player, action);
        var facing = Compass(game.StateOf(player.Id)?.Facing);
        if (!result.Success)
            return Reply(StatusCodes.Status200OK,
                ControllerResponse.Rejected(result.Error!.Value.ToHttp().Message) with { Facing = facing });

        var body = result switch
        {
            { Scan: { } scan } => ControllerResponse.Scanned(scan.OnMine, scan.AdjacentMines),
            { Dig: { } dig } => ControllerResponse.Dug(DigEvent(dig)),
            { Mine: { } mine } => ControllerResponse.Stepped(MineEvent(mine.Mine)),
            _ => ControllerResponse.Success() 
        };
        return Reply(StatusCodes.Status200OK, body with { Facing = facing });
    }

    private static string? Compass(Direction? facing) => facing switch
    {
        Direction.Up => "N",
        Direction.Right => "E",
        Direction.Down => "S",
        Direction.Left => "W",
        _ => null,
    };

    private static string DigEvent(DigResult dig) => dig switch
    {
        { Found: TileContent.Treasure } => ControllerEvent.Treasure,
        { Triggered: true } => MineEvent(dig.Found),
        _ => ControllerEvent.None,
    };

    private static string MineEvent(TileContent mine) =>
        mine == TileContent.PointMine ? ControllerEvent.PointMine : ControllerEvent.FreezeMine;

    private static bool TryResolve(
        GameRegistry registry, string? pin, string? playerId, string? deviceId,
        [NotNullWhen(true)] out Game? game,
        [NotNullWhen(true)] out Player? player,
        [NotNullWhen(false)] out JsonHttpResult<ControllerResponse>? error
    )
    {
        player = null;
        error = null;

        game = string.IsNullOrWhiteSpace(pin) ? null : registry.Find(pin);
        if (game is null)
        {
            error = Reply(StatusCodes.Status404NotFound, ControllerResponse.Fail("Unknown PIN"));
            return false;
        }

        player = game.FindController(playerId, deviceId);
        if (player is null)
        {
            error = Reply(StatusCodes.Status403Forbidden, ControllerResponse.Fail("Unknown controller"));
            return false;
        }

        return true;
    }

    private static JsonHttpResult<ControllerResponse> Reply(int status, ControllerResponse body) => 
        TypedResults.Json(body, statusCode: status);
}