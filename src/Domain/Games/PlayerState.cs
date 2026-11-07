using Domain.Boards;

namespace Domain.Games;

public sealed class PlayerState(Position position, Direction facing)
{
    public Position Position { get; internal set; } = position;
    public Direction Facing { get; internal set; } = facing;
    public DateTimeOffset NextMoveAt { get; internal set; }
    public DateTimeOffset FrozenUntil { get; internal set; }
}