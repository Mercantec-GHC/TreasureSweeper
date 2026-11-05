using Microsoft.EntityFrameworkCore;
using Shared.Leaderboard;

namespace Infrastructure.Persistence;

public sealed class LeaderboardQueries(TreasureSweeperDbContext db) : ILeaderboardQueries
{
	public async Task<IReadOnlyList<LeaderboardEntryResponse>> GetTopScoresAsync(
		int limit = 10, CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, 100);
		return await db.RoundPlayers
			.AsNoTracking()
			// .Where(rp => rp.Round.PlayerCount >= 2)  // uncomment to exclude solo rounds (game-rules decision)
			.OrderByDescending(rp => rp.Score)
			.ThenBy(rp => rp.Round.EndedAt)
			.Take(limit)
			.Select(rp => new LeaderboardEntryResponse(rp.Player.Name, rp.Score, rp.Round.EndedAt))
			.ToListAsync(ct);
	}

	public async Task<RoundResultResponse?> GetRoundAsync(Guid roundId, CancellationToken ct = default)
	{
		return await db.Rounds
			.AsNoTracking()
			.Where(r => r.Id == roundId)
			.Select(r => new RoundResultResponse(
				r.Id, r.StartedAt, r.EndedAt, r.PlayerCount, r.EndReason,
				r.PlayerResults
					.OrderBy(rp => rp.Placement)
					.Select(rp => new RoundPlayerResultResponse(
						rp.Player.Name, rp.Score, rp.Placement,
						rp.TreasuresFound, rp.FreezeMinesHit, rp.PointMinesHit,
						rp.MinesAvoided, rp.TotalSteps, rp.FewestStepsToTreasure))
					.ToList()))
			.FirstOrDefaultAsync(ct);
	}
	public async Task<IReadOnlyList<PlayerStatResponse>> GetMostTreasuresAsync(
	int limit = 10, CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, 100);
		var rows = await db.RoundPlayers
			.AsNoTracking()
			.GroupBy(rp => rp.Player.Name)
			.Select(g => new { Name = g.Key, Total = g.Sum(x => x.TreasuresFound) })
			.OrderByDescending(x => x.Total)
			.Take(limit)
			.ToListAsync(ct);
		return rows.Select(x => new PlayerStatResponse(x.Name, x.Total)).ToList();
	}

	public async Task<IReadOnlyList<PlayerStatResponse>> GetFewestStepsToTreasureAsync(
		int limit = 10, CancellationToken ct = default)
	{
		limit = Math.Clamp(limit, 1, 100);
		var rows = await db.RoundPlayers
			.AsNoTracking()
			.Where(rp => rp.FewestStepsToTreasure != null)
			.GroupBy(rp => rp.Player.Name)
			.Select(g => new { Name = g.Key, Best = g.Min(x => x.FewestStepsToTreasure) })
			.OrderBy(x => x.Best)
			.Take(limit)
			.ToListAsync(ct);
		return rows.Select(x => new PlayerStatResponse(x.Name, x.Best ?? 0)).ToList();
	}
}