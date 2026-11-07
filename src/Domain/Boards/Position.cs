namespace Domain.Boards;

public readonly record struct Position(int X, int Y)
{
    // Chebyshev distance: diagonal neighbours count as 1.
    public int DistanceTo(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public Position Step(Direction d) => d switch 
    {
        Direction.Up      => this with { Y = Y - 1 },
        Direction.Down    => this with { Y = Y + 1 },
        Direction.Left    => this with { X = X - 1 },
        Direction.Right   => this with { X = X + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, null),
    };
}