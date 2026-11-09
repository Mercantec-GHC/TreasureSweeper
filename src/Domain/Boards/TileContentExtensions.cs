namespace Domain.Boards;

public static class TileContentExtensions
{
    private const int DISCOVERED_BIT = 0b1000_0000;
    private const int MINE_BIT       = 0b0100_0000;
    private const int CONTENT_MASK   = 0b0110_0000;
    private const int COUNT_MASK     = 0b0000_1111;

    public static bool IsMine(this TileContent t) => ((int)t & MINE_BIT) != 0;
    public static bool IsHole(this TileContent t) => t == TileContent.Hole;
    public static bool IsDiscovered(this TileContent t) => ((int)t & DISCOVERED_BIT) != 0;

    public static TileContent HiddenContent(this TileContent t) => (TileContent)((int)t & CONTENT_MASK);

    // A discovered tile shows either its content (treasure/mine) or, if it has none, the mine count.
    public static TileContent Discovered(this TileContent t, int adjacentMines) =>
        t.HiddenContent() == TileContent.Empty
            ? (TileContent)(DISCOVERED_BIT | adjacentMines)
            : (TileContent)(DISCOVERED_BIT | (int)t.HiddenContent());
}