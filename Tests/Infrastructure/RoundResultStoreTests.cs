using Infrastructure;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shared.Games;

namespace Tests.Infrastructure;

public class RoundResultStoreTests : IDisposable
{
	private readonly SqliteConnection _connection;
	private readonly DbContextOptions<TreasureSweeperDbContext> _options;

	public RoundResultStoreTests()
	{
		// In-memory SQLite lives only while the connection is open.
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();
		_options = new DbContextOptionsBuilder<TreasureSweeperDbContext>()
			.UseSqlite(_connection)
			.Options;
		using var ctx = new TreasureSweeperDbContext(_options);
		ctx.Database.EnsureCreated();   // build the schema from the model
	}

	private TreasureSweeperDbContext NewContext() => new(_options);
	public void Dispose() => _connection.Dispose();

	private static FinishedPlayer P(string name, string? device, int score, int placement,
		int treasures = 0, int steps = 0, int? fewest = null) =>
		new(device, name, score, placement, treasures, 0, 0, 0, steps, fewest);

	[Fact]
	public async Task SaveAsync_StoresRoundAndResults()
	{
		var round = new FinishedRound(
			DateTimeOffset.UtcNow.AddMinutes(-8), DateTimeOffset.UtcNow, RoundEndReason.TimeUp,
			new[] { P("Alpha", "DEV_A", 100, 1, treasures: 2), P("Bravo", "DEV_B", 40, 2) });

		await new RoundResultStore(NewContext()).SaveAsync(round);

		await using var ctx = NewContext();
		Assert.Equal(1, await ctx.Rounds.CountAsync());
		Assert.Equal(2, await ctx.Players.CountAsync());
		Assert.Equal(2, await ctx.RoundPlayers.CountAsync());
		var alpha = await ctx.RoundPlayers.Include(rp => rp.Player)
			.SingleAsync(rp => rp.Player.Name == "Alpha");
		Assert.Equal(100, alpha.Score);
		Assert.Equal(2, alpha.TreasuresFound);
	}

	[Fact]
	public async Task SaveAsync_SameDevice_ReusesPersistentPlayer()
	{
		var alpha = P("Alpha", "DEV_A", 10, 1);
		await new RoundResultStore(NewContext()).SaveAsync(new FinishedRound(
			DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, RoundEndReason.TimeUp, new[] { alpha }));
		await new RoundResultStore(NewContext()).SaveAsync(new FinishedRound(
			DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, RoundEndReason.AllTreasuresFound, new[] { alpha }));

		await using var ctx = NewContext();
		Assert.Equal(1, await ctx.Players.CountAsync());       // same device → one persistent player
		Assert.Equal(2, await ctx.Rounds.CountAsync());
		Assert.Equal(2, await ctx.RoundPlayers.CountAsync());  // two results for that player
	}

	[Fact]
	public async Task Leaderboard_TopScore_OrdersDescending()
	{
		await new RoundResultStore(NewContext()).SaveAsync(new FinishedRound(
			DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, RoundEndReason.TimeUp,
			new[] { P("Alpha", "DEV_A", 100, 2), P("Bravo", "DEV_B", 250, 1) }));

		await using var ctx = NewContext();
		var top = await ctx.RoundPlayers.Include(rp => rp.Player)
			.OrderByDescending(rp => rp.Score).Select(rp => rp.Player.Name).FirstAsync();
		Assert.Equal("Bravo", top);
	}
}