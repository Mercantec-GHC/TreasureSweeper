namespace Domain.Boards;

public readonly record struct Position(int X, int Y)
{
    // Chebyshev distance: diagonal neighbours count as 1.
    public int DistanceTo(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
}