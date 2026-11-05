using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Leaderboard;

namespace API.Endpoints;

internal static class LeaderboardEndpoints
{
	public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/leaderboard").WithTags("Leaderboard");

		group.MapGet("/top-scores", GetTopScores)
			.WithName("GetTopScores")
			.WithSummary("All-time top scores (optional ?limit, default 10)");

		group.MapGet("/rounds/{id:guid}", GetRound)
			.WithName("GetRoundResult")
			.WithSummary("A single finished round's result");

		return app;
	}

	private static async Task<Ok<IReadOnlyList<LeaderboardEntryResponse>>> GetTopScores(
		ILeaderboardQueries queries, int limit = 10, CancellationToken ct = default)
	{
		var scores = await queries.GetTopScoresAsync(limit, ct);
		return TypedResults.Ok(scores);
	}

	private static async Task<Results<Ok<RoundResultResponse>, NotFound>> GetRound(
		Guid id, ILeaderboardQueries queries, CancellationToken ct = default)
	{
		var round = await queries.GetRoundAsync(id, ct);
		return round is null ? TypedResults.NotFound() : TypedResults.Ok(round);
	}
}