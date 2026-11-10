using Shared.Leaderboard;

namespace Infrastructure.Persistence;

public interface ILeaderboardQueries
{
	Task<IReadOnlyList<LeaderboardEntryResponse>> GetTopScoresAsync(int limit = 10, CancellationToken ct = default);
	Task<RoundResultResponse?> GetRoundAsync(Guid roundId, CancellationToken ct = default);
	Task<IReadOnlyList<PlayerStatResponse>> GetMostTreasuresAsync(int limit = 10, CancellationToken ct = default);
	Task<IReadOnlyList<PlayerStatResponse>> GetFewestStepsToTreasureAsync(int limit = 10, CancellationToken ct = default);
	Task<IReadOnlyList<RoundSummaryResponse>> GetRecentRoundsAsync(int limit = 20, CancellationToken ct = default);
}