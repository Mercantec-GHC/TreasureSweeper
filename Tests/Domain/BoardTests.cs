using Domain.Boards;

namespace Tests.Domain;

public class BoardTests
{
    private const int LAST = BoardSettings.SIZE - 1;
    private static readonly Position MIDDLE = new(10, 10);

    private static Board EmptyBoard() => 
        new(new TileContent[BoardSettings.SIZE, BoardSettings.SIZE], []);

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(LAST, 0, 3)]
    [InlineData(0, LAST, 3)]
    [InlineData(LAST, LAST, 3)]
    [InlineData(0, 10, 5)]
    [InlineData(LAST, 10, 5)]
    [InlineData(10, 0, 5)]
    [InlineData(10, LAST, 5)]
    [InlineData(10, 10, 8)]
    public void CountAdjacentMines_AllNeighboursMined_CountsOnlyTilesOnTheBoard(int x, int y, int expected)
    {
        var board = EmptyBoard();
        var p = new Position(x, y);
        foreach (var n in board.Neighbours(p)) board.Set(n, TileContent.PointMine);

        Assert.Equal(expected, board.Neighbours(p).Count());
        Assert.Equal(expected, board.CountAdjacentMines(p));
    }

    [Fact]
    public void CountAdjacentMines_NoMines_IsZero() =>
        Assert.Equal(0, EmptyBoard().CountAdjacentMines(MIDDLE));

    [Fact]
    public void CountAdjacentMines_FreezeAndPointMines_BothCount()
    {
        var board = EmptyBoard();
        board.Set(new(9, 9), TileContent.FreezeMine);
        board.Set(new(11, 11), TileContent.PointMine);

        Assert.Equal(2, board.CountAdjacentMines(MIDDLE));
    }

    [Fact]
    public void CountAdjacentMines_MineOnTheTileItself_IsNotCounted()
    {
        var board = EmptyBoard();
        board.Set(MIDDLE, TileContent.PointMine);

        Assert.Equal(0, board.CountAdjacentMines(MIDDLE));
    }

    [Fact]
    public void CountAdjacentMines_MinesTwoTilesAway_AreNotCounted()
    {
        var board = EmptyBoard();
        board.Set(new(12, 10), TileContent.PointMine);
        board.Set(new(8, 8), TileContent.FreezeMine);

        Assert.Equal(0, board.CountAdjacentMines(MIDDLE));
    }

    [Fact]
    public void CountAdjacentMines_TreasureAndHole_AreNotCounted()
    {
        var board = EmptyBoard();
        board.Set(new(9, 10), TileContent.Treasure);
        board.Set(new(11, 10), TileContent.Hole);

        Assert.Equal(0, board.CountAdjacentMines(MIDDLE));
    }

    [Fact]
    public void CountAdjacentMines_ScannedMine_StillCounts()
    {
        var board = EmptyBoard();
        board.Set(new(9, 10), TileContent.DiscoveredPointMine);

        Assert.Equal(1, board.CountAdjacentMines(MIDDLE));
    }

    [Fact]
    public void Reveal_EmptyTile_StoresCountAndDiscoveredFlag()
    {
        var board = EmptyBoard();
        board.Set(new(9, 9), TileContent.FreezeMine);
        board.Set(new(11, 9), TileContent.PointMine);

        Assert.Equal(2, board.Reveal(MIDDLE));
        Assert.Equal(TileContent.MineClose2, board[MIDDLE]);
    }

    [Theory]
    [InlineData(TileContent.Treasure, TileContent.DiscoveredTreasure)]
    [InlineData(TileContent.FreezeMine, TileContent.DiscoveredFreezeMine)]
    [InlineData(TileContent.PointMine, TileContent.DiscoveredPointMine)]
    public void Reveal_TileWithContent_ShowsContentNotCount(TileContent content, TileContent expected)
    {
        var board = EmptyBoard();
        board.Set(MIDDLE, content);
        board.Set(new(10, 11), TileContent.PointMine);

        Assert.Equal(1, board.Reveal(MIDDLE)); // the controller still gets the count
        Assert.Equal(expected, board[MIDDLE]);
    }

    [Fact]
    public void Reveal_Again_ReplacesOldCount()
    {
        var board = EmptyBoard();
        board.Set(new(9, 9), TileContent.PointMine);
        board.Set(new(11, 11), TileContent.FreezeMine);
        board.Reveal(MIDDLE);
        Assert.Equal(TileContent.MineClose2, board[MIDDLE]);

        board.Set(new(9, 9), TileContent.Empty);
        board.Reveal(MIDDLE);

        Assert.Equal(TileContent.MineClose1, board[MIDDLE]);
    }

    [Fact]
    public void Reveal_Hole_StaysHole()
    {
        var board = EmptyBoard();
        board.Set(MIDDLE, TileContent.Hole);

        board.Reveal(MIDDLE);

        Assert.Equal(TileContent.Hole, board[MIDDLE]);
    }
}