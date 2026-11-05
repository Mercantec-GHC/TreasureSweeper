namespace Shared.Games;

/// <summary>A player as shown on the shared screen.</summary>
public sealed record PlayerResponse(
    string Id,
    int Number,
    string Name,
    string Color,
    string Symbol
);