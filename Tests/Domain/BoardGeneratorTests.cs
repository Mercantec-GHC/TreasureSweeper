using Domain.Boards;

namespace Tests.Domain;

public class BoardGeneratorTests
{
    private const int SEEDS = 50;

    private static IEnumerable<Board> Boards(int players) =>
        Enumerable.Range(0, SEEDS).Select(seed => BoardGenerator.Generate(players, new Random(seed)));

    private static IEnumerable<(Position Pos, TileContent Content)> Tiles(Board board) =>
        from x in Enumerable.Range(0, board.Size)
        from y in Enumerable.Range(0, board.Size)
        let p = new Position(x, y)
        select (p, board[p]);

    private static int Count(Board board, TileContent content) =>
        Tiles(board).Count(t => t.Content == content);

    [Fact]
    public void Generate_ZeroPlayers_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoardGenerator.Generate(0, new Random(1)));
    }

    [Fact]
    public void Generate_Always_UsesConfiguredSize()
    {
        var board = BoardGenerator.Generate(1, new Random(1));

        Assert.Equal(BoardSettings.SIZE, board.Size);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(8, 7)]
    public void Generate_PlayerCount_PlacesPlayersMinusOneTreasures(int players, int expected)
    {
        foreach (var board in Boards(players))
            Assert.Equal(expected, Count(board, TileContent.Treasure));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Generate_Always_Places32FreezeAnd32PointMines(int players)
    {
        foreach (var board in Boards(players))
        {
            Assert.Equal(BoardSettings.FREEZE_MINES, Count(board, TileContent.FreezeMine));
            Assert.Equal(BoardSettings.POINT_MINES, Count(board, TileContent.PointMine));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    public void Generate_PlayerCount_CreatesOneSpawnPerPlayer(int players)
    {
        foreach (var board in Boards(players))
        {
            Assert.Equal(players, board.Spawns.Count);
            Assert.Equal(players, board.Spawns.Select(s => s.Position).Distinct().Count());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Generate_Always_PutsNoMineNextToStartTile(int players)
    {
        foreach (var board in Boards(players))
        {
            var mines = Tiles(board)
                .Where(t => t.Content is TileContent.FreezeMine or TileContent.PointMine)
                .Select(t => t.Pos);

            foreach (var mine in mines)
            foreach (var spawn in board.Spawns)
                Assert.True(mine.DistanceTo(spawn.Position) > BoardSettings.SAFE_RADIUS,
                    $"Mine at {mine} is next to spawn at {spawn.Position}");
        }
    }

    [Fact]
    public void Generate_EightPlayers_SpreadsStartPositions()
    {
        foreach (var board in Boards(8))
        {
            var spawns = board.Spawns.Select(s => s.Position).ToList();
            var closest = spawns
                .SelectMany((a, i) => spawns.Skip(i + 1).Select(b => a.DistanceTo(b)))
                .Min();
            
            Assert.True(closest >= 5, $"Two spawns are only {closest} units apart");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Generate_Always_FacesSpawnsAwayFromWall(int players)
    {
        foreach (var board in Boards(players))
        foreach (var spawn in board.Spawns)
        {
            var next = spawn.Facing switch
            {
                Direction.Up => spawn.Position with { Y = spawn.Position.Y - 1 },
                Direction.Down => spawn.Position with { Y = spawn.Position.Y + 1 },
                Direction.Left => spawn.Position with { X = spawn.Position.X - 1 },
                _ => spawn.Position with { X = spawn.Position.X + 1 },
            };

            Assert.True(board.Contains(next), $"Spawn at {spawn.Position} faces {spawn.Facing} into the wall");
        }
    }

    [Fact]
    public void Generate_SameSeed_ProducesSameBoard()
    {
        var a = BoardGenerator.Generate(8, new Random(42));
        var b = BoardGenerator.Generate(8, new Random(42));
        
        Assert.Equal(a.Spawns, b.Spawns);
        Assert.Equal(Tiles(a), Tiles(b));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Generate_Always_LeavesStartTilesEmpty(int players)
    {
        foreach (var board in Boards(players))
        foreach (var spawn in board.Spawns)
            Assert.Equal(TileContent.Empty, board[spawn.Position]);
    }

}