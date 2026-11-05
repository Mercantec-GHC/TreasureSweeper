namespace Domain.Boards;

public static class BoardSettings
{
    public const int SIZE = 32;
    public const int FREEZE_MINES = 32;
    public const int POINT_MINES = 32;
    public const int SAFE_RADIUS = 1;
    public static int TreasuresFor(int players) => Math.Max(1, players - 1);
}