namespace Shared.Leaderboard;

/// <summary>One entry in the all-time top-scores board.</summary>
public sealed record LeaderboardEntryResponse(
	string PlayerName,
	int Score,
	DateTimeOffset AchievedAt);