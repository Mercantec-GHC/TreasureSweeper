using Shared.Games;

namespace Shared.Leaderboard;

/// <summary>A finished round for the history list: who won, when, and how it ended.</summary>
public sealed record RoundSummaryResponse(
    Guid Id,
    DateTimeOffset EndedAt,
    int PlayerCount,
    RoundEndReason EndReason,
    string? WinnerName,
    int WinnerScore);
