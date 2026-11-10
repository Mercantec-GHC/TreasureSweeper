using Domain.Games;

namespace Blazor.Realtime;

/// <summary>
/// Wraps a single <see cref="Game"/> and pushes its state to subscribed components.
/// Inputs (<see cref="Game.Changed"/>) are applied immediately by flagging the view dirty.
/// Output is coalesced and published at most ~10 times per second, and only when something
/// changed. The coalescing timer runs only while at least one component is subscribed, so a
/// game with no viewers is never ticked. The end-of-round state is published exactly once.
/// </summary>
public sealed class GamePublisher : IDisposable
{
	// ~10 Hz. A higher rate costs a lot and gives little visible gain.
	private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(100);

	private readonly Game _game;
	private readonly TimeProvider _time;
	private readonly Lock _gate = new();
	private readonly List<Func<Task>> _subscribers = [];

	private ITimer? _timer;
	private volatile bool _dirty;
	private int _lastSecondsRemaining = int.MinValue;
	private bool _endPublished;

	public GamePublisher(Game game, TimeProvider? time = null)
	{
		_game = game;
		_time = time ?? TimeProvider.System;
		_game.Changed += OnGameChanged;
	}

	private void OnGameChanged() => _dirty = true;

	/// <summary>
	/// Registers a component callback invoked (on a background thread) whenever the view
	/// should refresh. Dispose the returned token to unsubscribe safely when the circuit closes.
	/// </summary>
	public IDisposable Subscribe(Func<Task> onPublish)
	{
		ArgumentNullException.ThrowIfNull(onPublish);
		lock (_gate)
		{
			_subscribers.Add(onPublish);
			// A new (or reconnected) subscriber must see the current state right away.
			_dirty = true;
			if (_subscribers.Count == 1) StartTimer();
		}
		return new Subscription(this, onPublish);
	}

	private void Unsubscribe(Func<Task> onPublish)
	{
		lock (_gate)
		{
			_subscribers.Remove(onPublish);
			if (_subscribers.Count == 0) StopTimer();
		}
	}

	private void StartTimer() =>
		_timer ??= _time.CreateTimer(_ => Tick(), null, PublishInterval, PublishInterval);

	private void StopTimer()
	{
		_timer?.Dispose();
		_timer = null;
	}

	private void Tick()
	{
		MarkCountdownAndEnd();

		if (!_dirty) return;
		_dirty = false;

		Func<Task>[] subscribers;
		lock (_gate)
		{
			if (_subscribers.Count == 0) return;
			subscribers = [.. _subscribers];
		}

		foreach (var subscriber in subscribers)
			_ = InvokeSafely(subscriber);
	}

	// The visible countdown only changes once per second; flag the view dirty on that boundary
	// so the timer updates without publishing 10x/second of identical frames. The end-of-round
	// state is flagged exactly once.
	private void MarkCountdownAndEnd()
	{
		if (_game.Phase == GamePhase.Playing)
		{
			_endPublished = false;
			if (_game.EndsAt is { } endsAt)
			{
				var remaining = (int)Math.Ceiling((endsAt - _time.GetUtcNow()).TotalSeconds);
				if (remaining != _lastSecondsRemaining)
				{
					_lastSecondsRemaining = remaining;
					_dirty = true;
				}
			}
		}
		else if (_game.Phase == GamePhase.Ended && !_endPublished)
		{
			_endPublished = true;
			_dirty = true;
		}
	}

	private static async Task InvokeSafely(Func<Task> subscriber)
	{
		try
		{
			await subscriber();
		}
		catch
		{
			// A disposed circuit can throw while publishing; the component will unsubscribe.
		}
	}

	public void Dispose()
	{
		_game.Changed -= OnGameChanged;
		StopTimer();
	}

	private sealed class Subscription(GamePublisher publisher, Func<Task> onPublish) : IDisposable
	{
		private GamePublisher? _publisher = publisher;

		public void Dispose()
		{
			_publisher?.Unsubscribe(onPublish);
			_publisher = null;
		}
	}
}
