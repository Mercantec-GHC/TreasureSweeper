using Domain.Boards;

namespace Domain.Games;

public enum GameError
{
    GameFull,
    RoundInProgress,
    NoPlayers,
    RoundNotWaiting,
    RoundNotPlaying,
    Frozen,
    OnCooldown,
    Blocked,
}

public sealed record GameResult(GameError? Error, ScanResult? Scan = null, DigResult? Dig = null)
{
    public bool Success => Error is null;
    public static GameResult Ok { get; } = new((GameError?)null);
    public static GameResult Fail(GameError error) => new(error);
    public static GameResult Scanned(ScanResult scan) => new(null, scan);
    public static GameResult Dug(DigResult dig) => new(null, Dig: dig);
}

public sealed record ScanResult(Position Tile, int AdjacentMines, bool OnMine);

public sealed record DigResult(Position Tile, TileContent Found, int Points); 

public sealed record JoinResult(Player? Player, bool Reconnected, GameError? Error)
{
    public bool Success => Error is null;
    public static JoinResult Joined(Player p) => new(p, false, null);
    public static JoinResult Rejoined(Player p) => new(p, true, null);
    public static JoinResult Fail(GameError error) => new(null, false, error);
}

public enum CreateGameError
{
    InvalidPin,
    PinInUse,
    NoPinAvailable,
}

public sealed record CreateGameResult(Game? Game, CreateGameError? Error)
{
    public bool Success => Error is null;
    public static CreateGameResult Created(Game g) => new(g, null);
    public static CreateGameResult Fail(CreateGameError error) => new(null, error);
}