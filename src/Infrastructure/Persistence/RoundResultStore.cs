using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class RoundResultStore(TreasureSweeperDbContext db) : IRoundResultStore
{
	public async Task SaveAsync(FinishedRound round, CancellationToken ct = default)
	{
		var roundEntity = new RoundEntity
		{
			Id = Guid.NewGuid(),
			StartedAt = round.StartedAt,
			EndedAt = round.EndedAt,
			PlayerCount = round.PlayerCount,
			EndReason = round.EndReason,
		};

		foreach (var p in round.Players)
		{
			var player = await GetOrCreatePlayerAsync(p, ct);
			roundEntity.PlayerResults.Add(new RoundPlayerEntity
			{
				Round = roundEntity,
				Player = player,
				Score = p.Score,
				Placement = p.Placement,
				TreasuresFound = p.TreasuresFound,
				FreezeMinesHit = p.FreezeMinesHit,
				PointMinesHit = p.PointMinesHit,
				MinesAvoided = p.MinesAvoided,
				TotalSteps = p.TotalSteps,
				FewestStepsToTreasure = p.FewestStepsToTreasure,
			});
		}

		db.Rounds.Add(roundEntity);
		await db.SaveChangesAsync(ct);
	}

	// Reuse the persistent player for a known device, otherwise create a new one.
	private async Task<PlayerEntity> GetOrCreatePlayerAsync(FinishedPlayer p, CancellationToken ct)
	{
		if (p.DeviceId is not null)
		{
			var existing = await db.Players.FirstOrDefaultAsync(e => e.DeviceId == p.DeviceId, ct);
			if (existing is not null) return existing;
		}

		var created = new PlayerEntity
		{
			Id = Guid.NewGuid(),
			Name = p.Name,
			DeviceId = p.DeviceId,
			CreatedAt = DateTimeOffset.UtcNow,
		};
		db.Players.Add(created);
		return created;
	}
}