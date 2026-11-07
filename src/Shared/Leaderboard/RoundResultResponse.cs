using Shared.Games;

namespace Shared.Leaderboard;

/// <summary>A single finished round and its per-player results.</summary>
public sealed record RoundResultResponse(
	Guid Id,
	DateTimeOffset StartedAt,
	DateTimeOffset EndedAt,
	int PlayerCount,
	RoundEndReason EndReason,
	IReadOnlyList<RoundPlayerResultResponse> Players);

/// <summary>One player's result within a round.</summary>
public sealed record RoundPlayerResultResponse(
	string PlayerName,
	int Score,
	int Placement,
	int TreasuresFound,
	int FreezeMinesHit,
	int PointMinesHit,
	int MinesAvoided,
	int TotalSteps,
	int? FewestStepsToTreasure);