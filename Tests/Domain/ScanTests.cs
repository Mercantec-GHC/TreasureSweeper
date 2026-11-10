// Tests/Domain/ScanTests.cs
using Domain.Boards;
using Domain.Games;
using Tests.Support;

namespace Tests.Domain;

public class ScanTests
{
    private static readonly Position MIDDLE = new(10, 10);
    private static readonly Position IN_FRONT = new(10, 9); // facing Up
    private static readonly PlayerAction SCAN = new(ActionKind.Scan);

    private static (Game Game, Player Player, PlayerState State, FakeTime Time) StartedSolo()
    {
        var time = new FakeTime();
        var game = new Game("1234", time);
        var player = game.Join("P1", "DEV_1").Player!;
        game.Start(new Random(1));

        for (var x = 0; x < game.Board!.Size; x++)
        for (var y = 0; y < game.Board.Size; y++)
            game.Board.Set(new(x, y), TileContent.Empty);

        var state = game.StateOf(player.Id)!;
        state.Position = MIDDLE;
        state.Facing = Direction.Up;
        return (game, player, state, time);
    }

    [Fact]
    public void Scan_RevealsTileInFront_NotOwnTile()
    {
        var (game, player, _, _) = StartedSolo();
        game.Board!.Set(new(9, 8), TileContent.PointMine); // next to IN_FRONT, not to MIDDLE

        var scan = game.Act(player, SCAN).Scan!;

        Assert.Equal(IN_FRONT, scan.Tile);
        Assert.Equal(1, scan.AdjacentMines);
        Assert.False(scan.OnMine);
        Assert.Equal(TileContent.MineClose1, game.Board[IN_FRONT]);
        Assert.False(game.Board[MIDDLE].IsDiscovered());
    }

    [Theory]
    [InlineData(Direction.Up, 10, 9)]
    [InlineData(Direction.Right, 11, 10)]
    [InlineData(Direction.Down, 10, 11)]
    [InlineData(Direction.Left, 9, 10)]
    public void Scan_AnyFacing_ScansTheTileInFront(Direction facing, int x, int y)
    {
        var (game, player, state, _) = StartedSolo();
        state.Facing = facing;

        Assert.Equal(new Position(x, y), game.Act(player, SCAN).Scan!.Tile);
    }

    [Fact]
    public void Scan_MineInFront_ReportsOnMineAndKeepsTheMine()
    {
        var (game, player, _, _) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.FreezeMine);

        var scan = game.Act(player, SCAN).Scan!;

        
        Assert.True(scan.OnMine);
        Assert.Equal(0, scan.AdjacentMines);
        Assert.Equal(TileContent.FreezeMine, game.Board[IN_FRONT].HiddenContent());
    }

    [Fact]
    public void Scan_SameMineTwice_IsRecordedOnce()
    {
        var (game, player, state, time) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.PointMine);

        game.Act(player, SCAN);
        time.Advance(Game.SCAN_COOLDOWN);
        game.Act(player, SCAN);

        Assert.Equal(IN_FRONT, Assert.Single(state.ScannedMines));
    }

    [Fact]
    public void Scan_SafeTile_IsNotRecordedAsMine()
    {
        var (game, player, state, _) = StartedSolo();

        game.Act(player, SCAN);

        Assert.Empty(state.ScannedMines);
        Assert.Equal(IN_FRONT, state.LastScan!.Tile);
    }

    [Fact]
    public void Scan_FacingEdge_IsBlockedAndStartsNoCooldown()
    {
        var (game, player, state, _) = StartedSolo();
        state.Position = new(0, 0);

        Assert.Equal(GameError.Blocked, game.Act(player, SCAN).Error);

        state.Facing = Direction.Right;
        Assert.True(game.Act(player, SCAN).Success);
    }

    [Fact]
    public void Scan_Twice_SecondIsOnCooldownForOneSecond()
    {
        var (game, player, _, time) = StartedSolo();
        Assert.True(game.Act(player, SCAN).Success);

        time.Advance(Game.SCAN_COOLDOWN - TimeSpan.FromMilliseconds(1));
        Assert.Equal(GameError.OnCooldown, game.Act(player, SCAN).Error);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(game.Act(player, SCAN).Success);
    }

    [Fact]
    public void Scan_DoesNotBlockMove()
    {
        var (game, player, state, _) = StartedSolo();
        game.Act(player, SCAN);

        Assert.True(game.Act(player, new PlayerAction(ActionKind.Move)).Success);
        Assert.Equal(IN_FRONT, state.Position);
    }

    [Fact]
    public void Scan_DoesNotMoveOrTurnThePlayer()
    {
        var (game, player, state, _) = StartedSolo();

        game.Act(player, SCAN);

        Assert.Equal(MIDDLE, state.Position);
        Assert.Equal(Direction.Up, state.Facing);
    }

    [Fact]
    public void Scan_WhileFrozen_IsRejected()
    {
        var (game, player, state, time) = StartedSolo();
        state.FrozenUntil = time.Now + TimeSpan.FromSeconds(5);

        Assert.Equal(GameError.Frozen, game.Act(player, SCAN).Error);
        Assert.False(game.Board![IN_FRONT].IsDiscovered());
    }

    [Fact]
    public void Scan_BeforeRound_IsRejected()
    {
        var game = new Game("1234");
        var player = game.Join("P1", "DEV_1").Player!;

        Assert.Equal(GameError.RoundNotPlaying, game.Act(player, SCAN).Error);
    }

    [Fact]
    public void Scan_RaisesChangedOnlyOnSuccess()
    {
        var (game, player, _, _) = StartedSolo();
        var changes = 0;
        game.Changed += () => changes++;

        game.Act(player, SCAN); // ok
        game.Act(player, SCAN); // cooldown

        Assert.Equal(1, changes);
    }
}