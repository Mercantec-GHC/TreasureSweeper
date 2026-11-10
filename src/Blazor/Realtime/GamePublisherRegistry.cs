using System.Collections.Concurrent;
using Domain.Games;

namespace Blazor.Realtime;

/// <summary>
/// Singleton that hands out one <see cref="GamePublisher"/> per <see cref="Game"/>, created
/// lazily on first use so games are only wired up once a component starts watching them.
/// </summary>
public sealed class GamePublisherRegistry(TimeProvider? time = null) : IDisposable
{
	private readonly ConcurrentDictionary<Game, GamePublisher> _publishers = new();
	private readonly TimeProvider? _time = time;

	public GamePublisher For(Game game) =>
		_publishers.GetOrAdd(game, g => new GamePublisher(g, _time));

	public void Dispose()
	{
		foreach (var publisher in _publishers.Values)
			publisher.Dispose();
		_publishers.Clear();
	}
}
