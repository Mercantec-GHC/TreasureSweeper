namespace Shared.Games;

/// <summary>The current state of a game lobby.</summary>
public sealed record GameResponse(
    string Pin,
    GamePhase Phase,
    int MaxPlayers,
    IReadOnlyList<PlayerResponse> Players
);