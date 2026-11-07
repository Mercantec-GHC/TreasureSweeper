namespace Domain.Boards;


public sealed class Board
{
    private readonly TileContent[,] _tiles;

    internal Board(TileContent[,] tiles, IReadOnlyList<Spawn> spawns)
    {
        _tiles = tiles;
        Size = tiles.GetLength(0);
        Spawns = spawns;
    }
    public int Size { get; }
    public IReadOnlyList<Spawn> Spawns { get; }
    public TileContent this[Position p] => _tiles[p.X, p.Y];
    public bool Contains(Position p) => 
        p.X >= 0 && p.X < Size && p.Y >= 0 && p.Y < Size;

    public IEnumerable<Position> Neighbours(Position p)
{
    for (var dx = -1; dx <= 1; dx++)
    for (var dy = -1; dy <= 1; dy++)
    {
        if (dx == 0 && dy == 0) continue;
        var n = new Position(p.X + dx, p.Y + dy);
        if (Contains(n)) yield return n;
    }
}

    public int CountAdjacentMines(Position p) => Neighbours(p).Count(n => this[n].IsMine());

    internal void Set(Position p, TileContent content) => _tiles[p.X, p.Y] = content;

    internal int Reveal(Position p)
    {
        var count = CountAdjacentMines(p);
        if (!this[p].IsHole()) Set(p, this[p].Discovered(count));
        return count;
    }
    
    private static Board BoardWith(params (Position Pos, TileContent Content)[] tiles)
    {
        var grid = new TileContent[BoardSettings.SIZE, BoardSettings.SIZE];
        foreach (var (p, c) in tiles) grid[p.X, p.Y] = c;
        return new Board(grid, []);
    }
}