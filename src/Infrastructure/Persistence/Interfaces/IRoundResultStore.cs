using Shared.Games;

namespace Infrastructure.Persistence.Interfaces;

/// <summary>
/// Saves a finished round and its per-player results. The caller (the game host)
/// builds a FinishedRound from the in-memory game state when a round ends.
/// Only finished rounds are stored; live game state stays in memory.
/// </summary>
public interface IRoundResultStore
{
	Task SaveAsync(FinishedRound round, CancellationToken ct = default);
}

public sealed record FinishedRound(
	DateTimeOffset StartedAt,
	DateTimeOffset EndedAt,
	RoundEndReason EndReason,
	IReadOnlyList<FinishedPlayer> Players)
{
	public int PlayerCount => Players.Count;
}

/// <summary>One player's result in a finished round.</summary>
public sealed record FinishedPlayer(
	string? DeviceId,
	string Name,
	int Score,
	int Placement,
	int TreasuresFound,
	int FreezeMinesHit,
	int PointMinesHit,
	int MinesAvoided,
	int TotalSteps,
	int? FewestStepsToTreasure);