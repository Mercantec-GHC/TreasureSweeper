namespace Shared.Games;

/// <summary>Optional settings when creating a game.</summary>
public sealed record CreateGameRequest
{
    /// <summary>A fixed PIN (4–8 digits). Leave empty to get a random 4-digit PIN.</summary>
    public string? Pin { get; init; }
}