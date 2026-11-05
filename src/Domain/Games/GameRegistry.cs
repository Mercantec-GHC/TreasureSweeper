using System.Collections.Concurrent;

namespace Domain.Games;

public sealed class GameRegistry
{
    private const int MAX_PIN_ATTEMPTS = 20;
    private readonly ConcurrentDictionary<string, Game> _games = new();

    public CreateGameResult Create(string? requestedPin = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedPin))
        {
            var pin = requestedPin.Trim();
            if (pin.Length is < 4 or > 8 || !pin.All(char.IsAsciiDigit))
                return CreateGameResult.Fail(CreateGameError.InvalidPin);
            
            var game = new Game(pin);
            return _games.TryAdd(pin, game)
                ? CreateGameResult.Created(game)
                : CreateGameResult.Fail(CreateGameError.PinInUse);
        }

        for (var i = 0; i < MAX_PIN_ATTEMPTS; i++)
        {
            var pin = Random.Shared.Next(1000, 10000).ToString();
            var game = new Game(pin);
            if (_games.TryAdd(pin, game))
                return CreateGameResult.Created(game);
        }

        return CreateGameResult.Fail(CreateGameError.NoPinAvailable);
    }

    public Game? Find(string pin) =>
        _games.TryGetValue(pin.Trim(), out var game) ? game : null;
}