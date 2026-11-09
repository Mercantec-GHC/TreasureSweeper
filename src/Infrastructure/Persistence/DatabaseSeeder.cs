using Infrastructure.Persistence.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Games;

namespace Infrastructure.Persistence;

/// <summary>Development-only sample data so the UI and API can be tested without playing a round (#38).</summary>
public static class DatabaseSeeder
{
	public static async Task SeedAsync(IRoundResultStore store, TreasureSweeperDbContext db, CancellationToken ct = default)
	{
		if (await db.Rounds.AnyAsync(ct)) return;   // idempotent: only seed an empty database

		var t = DateTimeOffset.UtcNow.AddDays(-1);

		// Round 1 — 3 players, treasures ran out
		await store.SaveAsync(new FinishedRound(t, t.AddMinutes(8), RoundEndReason.AllTreasuresFound, new[]
		{
			new FinishedPlayer("SEED_ADA",   "Brave Otter", 220, 1, TreasuresFound: 3, FreezeMinesHit: 1, PointMinesHit: 0, MinesAvoided: 4, TotalSteps: 180, FewestStepsToTreasure: 12),
			new FinishedPlayer("SEED_LIN",   "Sly Fox",     130, 2, TreasuresFound: 2, FreezeMinesHit: 0, PointMinesHit: 2, MinesAvoided: 3, TotalSteps: 210, FewestStepsToTreasure: 20),
			new FinishedPlayer("SEED_GRACE", "Calm Heron",   40, 3, TreasuresFound: 1, FreezeMinesHit: 2, PointMinesHit: 1, MinesAvoided: 1, TotalSteps: 260, FewestStepsToTreasure: 33),
		}), ct);

		// Round 2 — same devices, ran out of time
		await store.SaveAsync(new FinishedRound(t.AddHours(1), t.AddHours(1).AddMinutes(8), RoundEndReason.TimeUp, new[]
		{
			new FinishedPlayer("SEED_LIN",   "Sly Fox",     300, 1, TreasuresFound: 4, FreezeMinesHit: 0, PointMinesHit: 0, MinesAvoided: 6, TotalSteps: 150, FewestStepsToTreasure: 9),
			new FinishedPlayer("SEED_ADA",   "Brave Otter", 170, 2, TreasuresFound: 2, FreezeMinesHit: 1, PointMinesHit: 1, MinesAvoided: 2, TotalSteps: 190, FewestStepsToTreasure: 15),
			new FinishedPlayer("SEED_GRACE", "Calm Heron",   90, 3, TreasuresFound: 1, FreezeMinesHit: 0, PointMinesHit: 0, MinesAvoided: 5, TotalSteps: 230, FewestStepsToTreasure: 28),
		}), ct);

		// Round 3 — a solo round
		await store.SaveAsync(new FinishedRound(t.AddHours(2), t.AddHours(2).AddMinutes(5), RoundEndReason.AllTreasuresFound, new[]
		{
			new FinishedPlayer("SEED_ADA", "Brave Otter", 100, 1, TreasuresFound: 1, FreezeMinesHit: 0, PointMinesHit: 0, MinesAvoided: 2, TotalSteps: 40, FewestStepsToTreasure: 7),
		}), ct);
	}
}