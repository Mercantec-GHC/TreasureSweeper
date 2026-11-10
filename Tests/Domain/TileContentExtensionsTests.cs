// Tests/Domain/TileContentExtensionsTests.cs
using Domain.Boards;

namespace Tests.Domain;

public class TileContentExtensionsTests
{
    [Theory]
    [InlineData(TileContent.FreezeMine, true)]
    [InlineData(TileContent.PointMine, true)]
    [InlineData(TileContent.DiscoveredFreezeMine, true)]
    [InlineData(TileContent.DiscoveredPointMine, true)]
    [InlineData(TileContent.Empty, false)]
    [InlineData(TileContent.Treasure, false)]
    [InlineData(TileContent.DiscoveredTreasure, false)]
    [InlineData(TileContent.Hole, false)]
    [InlineData(TileContent.MineClose0, false)]
    [InlineData(TileContent.MineClose8, false)]
    public void IsMine_MatchesOnlyFreezeAndPointMines(TileContent tile, bool expected) =>
        Assert.Equal(expected, tile.IsMine());

    [Fact]
    public void Discovered_PointMine_IgnoresCount()
    {
        Assert.Equal(TileContent.DiscoveredPointMine, TileContent.PointMine.Discovered(3));
    }

    [Fact]
    public void Discovered_AlreadyScannedTile_GetsNewCount()
    {
        Assert.Equal(TileContent.MineClose1, TileContent.MineClose2.Discovered(1));
    }

    [Theory]
    [InlineData(TileContent.Treasure, TileContent.Empty)]
    [InlineData(TileContent.FreezeMine, TileContent.Empty)]
    [InlineData(TileContent.PointMine, TileContent.Empty)]
    public void Visible_HidesUndugTreasureAndUndiscoveredMines(TileContent tile, TileContent expected) =>
        Assert.Equal(expected, tile.Visible());

    [Theory]
    [InlineData(TileContent.DiscoveredTreasure)]
    [InlineData(TileContent.DiscoveredFreezeMine)]
    [InlineData(TileContent.DiscoveredPointMine)]
    public void Visible_ShowsDiscoveredContent(TileContent tile) =>
        Assert.Equal(tile, tile.Visible());

    [Theory]
    [InlineData(TileContent.Empty, TileContent.Empty)]
    [InlineData(TileContent.Outside, TileContent.Outside)]
    [InlineData(TileContent.Hole, TileContent.Hole)]
    [InlineData(TileContent.MineClose0, TileContent.MineClose0)]
    [InlineData(TileContent.MineClose3, TileContent.MineClose3)]
    [InlineData(TileContent.MineClose8, TileContent.MineClose8)]
    public void Visible_KeepsDugTilesHolesAndScanNumbers(TileContent tile, TileContent expected) =>
        Assert.Equal(expected, tile.Visible());
}