using Domain.Boards;
using RoundEndReason = Shared.Games.RoundEndReason;

namespace Domain.Games;

public sealed class Game(string pin, TimeProvider? time = null)
{
    private readonly Lock _gate = new();
    public const int MAX_PLAYERS = 8;
    public static readonly TimeSpan MOVE_COOLDOWN = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan SCAN_COOLDOWN = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DIG_COOLDOWN = TimeSpan.FromSeconds(1);
    public const int TREASURE_START_POINTS = 1000;
    public const int POINT_MINE_PENALTY = 25;
    public static readonly TimeSpan FREEZE_DURATION = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ROUND_DURATION = TimeSpan.FromMinutes(8);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Dictionary<string, Player> _players = [];
    private readonly Dictionary<string, string> _devices = [];
    private readonly Dictionary<string, PlayerState> _states = [];
    private int _treasuresDug;
    private ITimer? _roundTimer;

    public event Action? Changed;
    
    public string Pin { get; } = pin;
    public Board? Board { get; private set; }
    public GamePhase Phase { get; private set; } = GamePhase.Waiting;
    public DateTimeOffset? EndsAt { get; private set; }
    public RoundEndReason? EndReason { get; private set; }
    public int TreasuresLeft => Board == null ? 0 : CountTreasures(Board);
    public IReadOnlyCollection<Player> Players => _players.Values;

    public JoinResult Join(string? name, string? deviceId)
    {
        JoinResult result;
        lock (_gate) result = JoinLocked(name, deviceId);
        if (result.Success) Changed?.Invoke();
        return result;
    }

    private JoinResult JoinLocked(string? name, string? deviceId)
    {
        var hasDevice = !string.IsNullOrWhiteSpace(deviceId);

        if (hasDevice && _devices.TryGetValue(deviceId!, out var existingId))
            return JoinResult.Rejoined(_players[existingId]);

        if (Phase == GamePhase.Playing)
            return JoinResult.Fail(GameError.RoundInProgress);

        if (_players.Count >= MAX_PLAYERS)
            return JoinResult.Fail(GameError.GameFull);

        var number = _players.Count + 1;
        var identity = PlayerIdentity.ForNumber(number);
        var player = new Player(
            Id: Guid.NewGuid().ToString("N"),
            Number: number,
            Name: string.IsNullOrWhiteSpace(name) ? $"Arduino {number}" : name.Trim(),
            Color: identity.Color,
            Symbol: identity.Symbol,
            DeviceId: hasDevice ? deviceId : null
        );

        _players.Add(player.Id, player);
        if (hasDevice) _devices.Add(deviceId!, player.Id);

        return JoinResult.Joined(player);
    }

    public Player? FindController(string? playerId, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || !_players.TryGetValue(playerId, out var player))
            return null;

        return player.DeviceId is null || player.DeviceId == deviceId ? player : null;
    }

    public GameResult Start(Random? rng = null) {
        GameResult result;
        lock (_gate) result = StartLocked(rng);
        if (result.Success) Changed?.Invoke();
        return result;
    }

    private GameResult StartLocked(Random? rng = null)
    {
        if (Phase != GamePhase.Waiting) return GameResult.Fail(GameError.RoundNotWaiting);
        if (_players.Count == 0) return GameResult.Fail(GameError.NoPlayers);
        Board = BoardGenerator.Generate(_players.Count, rng ?? Random.Shared);

        _states.Clear();
        _treasuresDug = 0;
        EndReason = null;
        foreach (var (player, spawn) in _players.Values.Zip(Board.Spawns))
            _states[player.Id] = new PlayerState(spawn.Position, spawn.Facing);

        Phase = GamePhase.Playing;
        EndsAt = _time.GetUtcNow() + ROUND_DURATION;
        _roundTimer = _time.CreateTimer(_ => End(RoundEndReason.TimeUp), null, ROUND_DURATION, Timeout.InfiniteTimeSpan);
        return GameResult.Ok;
    }

    public GameResult Act(Player player, PlayerAction action) {
        GameResult result;
        bool expired;
        bool finished;
        lock (_gate)
        {
            // Fallback in case the timer has not fired yet (or a fake TimeProvider is used).
            expired = Phase == GamePhase.Playing && _time.GetUtcNow() >= EndsAt && EndLocked(RoundEndReason.TimeUp).Success;
            result = ActLocked(player, action);
            finished = result.Success && Phase == GamePhase.Playing && result.Dig?.Found == TileContent.Treasure
                && CountTreasures(Board!) == 0
                && EndLocked(RoundEndReason.AllTreasuresFound).Success;
        }
        if (result.Success || expired || finished) Changed?.Invoke();
        return result;
    }

    private GameResult ActLocked(Player player, PlayerAction action)
    {    
        if (Phase != GamePhase.Playing) return GameResult.Fail(GameError.RoundNotPlaying);
        
        var state = _states[player.Id];
        var now = _time.GetUtcNow();
        if (now < state.FrozenUntil) return GameResult.Fail(GameError.Frozen);

        return action.Kind switch
        {
            ActionKind.Move => Move(state, now),
            ActionKind.Rotate => Rotate(state, action.Turn!.Value),
            ActionKind.Scan => Scan(state, now),
            ActionKind.Dig => Dig(state, now),
            _ => GameResult.Ok,
        };
    }

    private GameResult Move(PlayerState state, DateTimeOffset now)
    {
        
        if (now < state.NextMoveAt) return GameResult.Fail(GameError.OnCooldown);

        var target = state.Position.Step(state.Facing);
        if (!Board!.Contains(target) || Board[target].IsHole()) return GameResult.Fail(GameError.Blocked);

        state.Position = target;
        state.NextMoveAt = now + MOVE_COOLDOWN;

        var content = Board[target];
        var mine = content.HiddenContent();
        if (!mine.IsMine() || content.IsDiscovered()) return GameResult.Ok;

        var points = TriggerMine(state, target, mine, now);
        return GameResult.SteppedOnMine(new MineHit(target, mine, points));
    }

    private static GameResult Rotate(PlayerState state, Direction turn)
    {
        state.Facing = state.Facing.Turn(turn);
        return GameResult.Ok;
    }

    private GameResult Scan(PlayerState state, DateTimeOffset now)
    {
        if (now < state.NextScanAt) return GameResult.Fail(GameError.OnCooldown);

        var tile = state.Position.Step(state.Facing);
        if (!Board!.Contains(tile)) return GameResult.Fail(GameError.Blocked);

        state.NextScanAt = now + SCAN_COOLDOWN;
        var count = Board.Reveal(tile);
        var scan = new ScanResult(tile, count, Board[tile].IsMine());
        state.RecordScan(scan);
        return GameResult.Scanned(scan);
    }

    private GameResult Dig(PlayerState state, DateTimeOffset now)
    {
        var tile = state.Position.Step(state.Facing);
        if (!Board!.Contains(tile)) return GameResult.Fail(GameError.Blocked);

        if (now < state.NextDigAt) return GameResult.Fail(GameError.OnCooldown);
        state.NextDigAt = now + DIG_COOLDOWN;

        var content = Board[tile];

        if (content.IsHole())
        {
            Board.Set(tile, TileContent.Empty);
            return GameResult.Dug(new DigResult(tile, TileContent.Hole, 0));
        }

        var found = content.HiddenContent();

        if (found.IsMine())
        {
            if (content.IsDiscovered())
                return GameResult.Dug(new DigResult(tile, found, 0));

            var penalty = TriggerMine(state, tile, found, now);
            return GameResult.Dug(new DigResult(tile, found, penalty, Triggered: true));
        }

        if (found == TileContent.Empty)
        {
            Board.Set(tile, TileContent.Hole);
            return GameResult.Dug(new DigResult(tile, found, 0));
        }

        var points = TREASURE_START_POINTS - _treasuresDug * 100;
        _treasuresDug++;
        state.Score += points;
        state.TreasuresFound++;
        Board.Set(tile, TileContent.Hole);

        return GameResult.Dug(new DigResult(tile, found, points));
    }

    private int TriggerMine(PlayerState state, Position tile, TileContent mine, DateTimeOffset now)
    {
        var points = 0;
        if (mine == TileContent.PointMine)
        {
            points = -POINT_MINE_PENALTY;
            state.Score += points;
            state.PointMinesHit++;
        }
        else
        {
            state.FrozenUntil = now + FREEZE_DURATION;
            state.FreezeMinesHit++;
        }

        Board!.Set(tile, TileContent.Hole);
        return points;
    }

    public GameResult End() => End(RoundEndReason.TimeUp);

    private GameResult End(RoundEndReason reason) {
        GameResult result;
        lock (_gate) result = EndLocked(reason);
        if (result.Success) Changed?.Invoke();
        return result;
    }

    private GameResult EndLocked(RoundEndReason reason)
    {
        if (Phase != GamePhase.Playing) return GameResult.Fail(GameError.RoundNotPlaying);
        Phase = GamePhase.Ended;
        EndReason = reason;
        _roundTimer?.Dispose();
        _roundTimer = null;
        return GameResult.Ok;
    }

    public GameResult Reset() {
        GameResult result;
        lock (_gate) result = ResetLocked();
        if (result.Success) Changed?.Invoke();
        return result;
    }

    private GameResult ResetLocked()
    {
        if (Phase == GamePhase.Playing) return GameResult.Fail(GameError.RoundInProgress);
        Board = null; 
        EndsAt = null;
        _states.Clear();
        _treasuresDug = 0;
        EndReason = null;
        Phase = GamePhase.Waiting;
        return GameResult.Ok;
    }

    private static int CountTreasures(Board board)
    {
        var count = 0;
        for (var x = 0; x < board.Size; x++)
            for (var y = 0; y < board.Size; y++)
                if (board[new Position(x, y)].HiddenContent() == TileContent.Treasure) count++;
        return count;
    }

    public PlayerState? StateOf(string playerId) => _states.GetValueOrDefault(playerId);
}