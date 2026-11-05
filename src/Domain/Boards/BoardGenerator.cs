namespace Domain.Boards;

public static class BoardGenerator
{
    private const int SPAWN_CANDIDATES = 50;

    public static Board Generate(int playerCount, Random rng)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerCount, 1);

        var size = BoardSettings.SIZE;
        var tiles = new TileContent[size, size];
        var starts = PickStartPositions(playerCount, size, rng);
        var spawns = starts.Select(p => new Spawn(p, FaceCentre(p, size))).ToList();

        var free = AllPositions(size).Where(p => !starts.Contains(p)).ToArray();
        rng.Shuffle(free);

        var mineSpots = free.Where(p => starts.All(s => p.DistanceTo(s) > BoardSettings.SAFE_RADIUS));

        Place(tiles, mineSpots, TileContent.FreezeMine, BoardSettings.FREEZE_MINES);
        Place(tiles, mineSpots, TileContent.PointMine, BoardSettings.POINT_MINES);
        Place(tiles, free, TileContent.Treasure, BoardSettings.TreasuresFor(playerCount));

        return new Board(tiles, spawns);
    }

    private static List<Position> PickStartPositions(int count, int size, Random rng)
    {
        var starts = new List<Position> { RandomPosition(size, rng) };

        while (starts.Count < count)
        {
            Position best = default;
            int bestScore = -1;

            for (var i = 0; i < SPAWN_CANDIDATES; i++)
            {
                var candidate = RandomPosition(size, rng);
                var score = starts.Min(s => s.DistanceTo(candidate));
                if (score > bestScore) (best, bestScore) = (candidate, score);
            }
            starts.Add(best);
        }
        return starts;
    }

    private static void Place(TileContent[,] tiles, IEnumerable<Position> candidates, TileContent content, int count)
    {
        foreach (var p in candidates.Where(p => tiles[p.X, p.Y] == TileContent.Empty).Take(count))
        {
            tiles[p.X, p.Y] = content;
        }
    }

    private static Position RandomPosition(int size, Random rng) =>
        new(rng.Next(size), rng.Next(size));

    private static IEnumerable<Position> AllPositions(int size) => 
        from x in Enumerable.Range(0, size)
        from y in Enumerable.Range(0, size)
        select new Position(x, y);

    private static Direction FaceCentre(Position p, int size)
    {
        var centre = (size - 1) / 2.0;
        var dx = centre - p.X;
        var dy = centre - p.Y;
        return Math.Abs(dx) > Math.Abs(dy)
            ? dx > 0 ? Direction.Right : Direction.Left
            : dy > 0 ? Direction.Down : Direction.Up;
    }
}