using Shared.Games;
using DomainGames = Domain.Games;

namespace API.Mapping;

internal static class GameMappings
{
    public static GameResponse ToResponse(this DomainGames.Game game) => new(
        game.Pin,
        game.Phase.ToResponse(),
        DomainGames.Game.MAX_PLAYERS,
        game.Players.OrderBy(p => p.Number).Select(p => p.ToResponse(game.StateOf(p.Id))).ToList()
    );

    public static PlayerResponse ToResponse(this DomainGames.Player player, DomainGames.PlayerState? state = null) =>
        new(player.Id, player.Number, player.Name, player.Color, player.Symbol,
            state is null ? null : new PositionResponse(state.Position.X, state.Position.Y),
            state?.Facing.ToResponse());

    public static Direction ToResponse(this Domain.Boards.Direction direction) => direction switch
    {
        Domain.Boards.Direction.Up    => Direction.Up,
        Domain.Boards.Direction.Right => Direction.Right,
        Domain.Boards.Direction.Down  => Direction.Down,
        Domain.Boards.Direction.Left  => Direction.Left,
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