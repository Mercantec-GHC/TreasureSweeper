namespace Domain.Boards;

public static class DirectionExtensions
{
    public static Direction TurnRight(this Direction d) => (Direction)(((int)d + 1) % 4);
    public static Direction TurnLeft(this Direction d) => (Direction)(((int)d + 3) % 4);

    public static Direction Turn(this Direction d, Direction turn) => turn switch
    {
        Direction.Left => d.TurnLeft(),
        Direction.Right => d.TurnRight(),
        _ => throw new ArgumentOutOfRangeException(nameof(turn), "Only Left/Right are turns"),
    };
}