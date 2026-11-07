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

    public bool IsHole(Position p) => _tiles[p.X, p.Y] == TileContent.Hole;

    internal void Set(Position p, TileContent content) => _tiles[p.X, p.Y] = content;
}