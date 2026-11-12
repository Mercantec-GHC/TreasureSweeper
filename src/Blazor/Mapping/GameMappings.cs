using Shared.Games;
using static Domain.Boards.TileContentExtensions;
using DomainGames = Domain.Games;
using DomainBoards = Domain.Boards;

namespace API.Mapping;

internal static class GameMappings
{
    private const int HOLE_BIT = 0b0001_0000;

    public static GameResponse ToResponse(this DomainGames.Game game) => new(
        game.Pin,
        game.Phase.ToResponse(),
        DomainGames.Game.MAX_PLAYERS,
        game.Players.OrderBy(p => p.Number).Select(p => p.ToResponse(game.StateOf(p.Id), game.Board)).ToList()
    );

    public static PlayerResponse ToResponse(this DomainGames.Player player, 
        DomainGames.PlayerState? state = null, DomainBoards.Board? board = null) =>
        new(player.Id, player.Number, player.Name, player.Color, player.Symbol,
            state is null ? null : new PositionResponse(state.Position.X, state.Position.Y),
            state?.Facing.ToResponse(),
            state is null || board is null ? null : board.ViewFor(state),
            state?.Score);

    public static IReadOnlyList<int?> ViewFor(this DomainBoards.Board board, DomainGames.PlayerState viewer)
    {
        const int reach = DomainBoards.BoardSettings.VIEW_DISTANCE;
        const int size = reach * 2 + 1;
        var tiles = new int?[size * size];

        for (var row = 0; row < size; row++)
        for (var col = 0; col < size; col++)
        {
            var p = viewer.Position.Relative(viewer.Facing, forward: reach - row, right: col - reach);
            tiles[row * size + col] = board.Contains(p) ? Visible(board[p]) : null;
        }

        return tiles;
    }

    // FNV-1a over the tiles (null = 255), so the controller can skip redrawing an unchanged view.
    public static uint ViewHash(IReadOnlyList<int?> view)
    {
        var hash = 2166136261u;
        foreach (var tile in view)
            hash = (hash ^ (byte)(tile ?? 255)) * 16777619u;
        return hash;
    }

    private static int Visible(DomainBoards.TileContent t) =>
        t.IsDiscovered() ? (int)t : (int)t & HOLE_BIT;

    public static Direction ToResponse(this DomainBoards.Direction direction) => direction switch
    {
        DomainBoards.Direction.Up    => Direction.Up,
        DomainBoards.Direction.Right => Direction.Right,
        DomainBoards.Direction.Down  => Direction.Down,
        DomainBoards.Direction.Left  => Direction.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    public static GamePhase ToResponse(this DomainGames.GamePhase phase) => phase switch
    {
        DomainGames.GamePhase.Waiting => GamePhase.Waiting,
        DomainGames.GamePhase.Playing => GamePhase.Playing,
        DomainGames.GamePhase.Ended   => GamePhase.Ended,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static (int Status, string Message) ToHttp(this DomainGames.GameError error) => error switch
    {
        DomainGames.GameError.GameFull        => (StatusCodes.Status409Conflict, $"Game full (max {DomainGames.Game.MAX_PLAYERS})"),
        DomainGames.GameError.RoundInProgress => (StatusCodes.Status409Conflict, "Round in progress"),
        DomainGames.GameError.NoPlayers       => (StatusCodes.Status409Conflict, "No players have joined"),
        DomainGames.GameError.RoundNotWaiting => (StatusCodes.Status409Conflict, "Round already started"),
        DomainGames.GameError.RoundNotPlaying => (StatusCodes.Status409Conflict, "No round is running"),
        DomainGames.GameError.Frozen          => (StatusCodes.Status409Conflict, "Player is frozen"),
        DomainGames.GameError.OnCooldown      => (StatusCodes.Status429TooManyRequests, "Player is on cooldown"),
        DomainGames.GameError.Blocked         => (StatusCodes.Status409Conflict, "Player is blocked"),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };

    public static (int Status, string Message) ToHttp(this DomainGames.CreateGameError error) => error switch
    {
        DomainGames.CreateGameError.InvalidPin     => (StatusCodes.Status400BadRequest, "PIN must be 4-8 digits"),
        DomainGames.CreateGameError.PinInUse       => (StatusCodes.Status409Conflict, "PIN already in use"),
        DomainGames.CreateGameError.NoPinAvailable => (StatusCodes.Status503ServiceUnavailable, "No free PIN, try again"),
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
    };
}