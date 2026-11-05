using Shared.Leaderboard;

namespace Infrastructure.Persistence;

public interface ILeaderboardQueries
{
	Task<IReadOnlyList<LeaderboardEntryResponse>> GetTopScoresAsync(int limit = 10, CancellationToken ct = default);
	Task<RoundResultResponse?> GetRoundAsync(Guid roundId, CancellationToken ct = default);
}