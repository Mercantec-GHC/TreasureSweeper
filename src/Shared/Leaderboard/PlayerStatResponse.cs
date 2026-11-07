namespace Shared.Leaderboard;

/// <summary>A player's aggregated value for a leaderboard category (treasures, steps, …).</summary>
public sealed record PlayerStatResponse(string PlayerName, int Value);