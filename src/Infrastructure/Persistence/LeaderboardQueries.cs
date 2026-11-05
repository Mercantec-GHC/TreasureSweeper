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
}