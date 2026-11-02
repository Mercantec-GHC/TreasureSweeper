namespace Domain.Games;

public sealed class Game(string pin)
{
    public const int MAX_PLAYERS = 8;
    private readonly Dictionary<string, Player> _players = [];
    private readonly Dictionary<string, string> _devices = [];
    
    public string Pin { get; } = pin;
    public GamePhase Phase { get; private set; } = GamePhase.Waiting;
    public IReadOnlyCollection<Player> Players => _players.Values;

    public JoinResult Join(string? name, string? deviceId)
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

    public GameResult Start()
    {
        if (Phase != GamePhase.Waiting) return GameResult.Fail(GameError.RoundNotWaiting);
        if (_players.Count == 0) return GameResult.Fail(GameError.NoPlayers);
        Phase = GamePhase.Playing;
        return GameResult.Ok;
    }

    public GameResult End()
    {
        if (Phase != GamePhase.Playing) return GameResult.Fail(GameError.RoundNotPlaying);
        Phase = GamePhase.Ended;
        return GameResult.Ok;
    }

    public GameResult Reset()
    {
        if (Phase == GamePhase.Playing) return GameResult.Fail(GameError.RoundInProgress);
        Phase = GamePhase.Waiting;
        return GameResult.Ok;
    }
}