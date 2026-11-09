using Domain.Boards;

namespace Domain.Games;

public sealed class PlayerState(Position position, Direction facing)
{
    public Position Position { get; internal set; } = position;
    public Direction Facing { get; internal set; } = facing;
    public DateTimeOffset NextMoveAt { get; internal set; }
    public DateTimeOffset NextScanAt { get; internal set; }
    public DateTimeOffset FrozenUntil { get; internal set; }

    private readonly HashSet<Position> _scannedMines = [];
    public ScanResult? LastScan { get; private set; }
    public IReadOnlySet<Position> ScannedMines => _scannedMines;

    internal void RecordScan(ScanResult scan)
    {
        LastScan = scan;
        if (scan.OnMine) _scannedMines.Add(scan.Tile);
    }
}