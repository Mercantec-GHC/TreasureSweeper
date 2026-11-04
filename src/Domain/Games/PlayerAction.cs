using Domain.Boards;

namespace Domain.Games;

public enum ActionKind { Move, Rotate, Scan, Dig }

public sealed record PlayerAction(ActionKind Kind, Direction? Turn = null)
{
    public static PlayerAction? Parse(string? action, string? direction) => 
        action?.Trim().ToLowerInvariant() switch
        {
            // Always forward relative to facing; any direction the firmware sends is ignored.
            "move" => new(ActionKind.Move),
            "rotate" => ParseTurn(direction) is { } turn ? new(ActionKind.Rotate, turn) : null,
            "scan" => new(ActionKind.Scan),
            "dig" => new(ActionKind.Dig),
            _ => null
        };

    private static Direction? ParseTurn(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "LEFT" => Direction.Left,
        "RIGHT" => Direction.Right,
        _ => null
    };
}