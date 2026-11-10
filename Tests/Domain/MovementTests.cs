using Domain.Boards;
using Domain.Games;

namespace Tests.Domain;

public class MovementTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now += by;
    }

    private static readonly Position MIDDLE = new(10, 10);

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

    private static readonly PlayerAction MOVE = new(ActionKind.Move);

    [Theory]
    [InlineData(Direction.Up, Direction.Right, Direction.Left)]
    [InlineData(Direction.Left, Direction.Up, Direction.Down)]
    public void Turn_LeftAndRight_RotatesClockwiseAndBack(Direction start, Direction right, Direction left)
    {
        Assert.Equal(right, start.TurnRight());
        Assert.Equal(left, start.TurnLeft());
    }

    [Theory]
    [InlineData(Direction.Up, 5, 4)]
    [InlineData(Direction.Down, 5, 6)]
    [InlineData(Direction.Left, 4, 5)]
    [InlineData(Direction.Right, 6, 5)]
    public void Step_EachDirection_MovesOneTile(Direction d, int x, int y)
    {
        Assert.Equal(new Position(x, y), new Position(5, 5).Step(d));
    }

    [Fact]
    public void Start_PlacesEachPlayerOnASpawn()
    {
        var game = new Game("1234");
        game.Join("P1", "DEV_1");
        game.Join("P2", "DEV_2");
        game.Start(new Random(1));

        var positions = game.Players.Select(p => game.StateOf(p.Id)!.Position);
        Assert.Equal(game.Board!.Spawns.Select(s => s.Position), positions);
    }

    [Fact]
    public void Move_BeforeStart_IsRejected()
    {
        var game = new Game("1234");
        var player = game.Join("P1", "DEV_1").Player!;

        Assert.Equal(GameError.RoundNotPlaying, game.Act(player, MOVE).Error);
    }

    [Fact]
    public void Move_StepsOneTileInFacingDirection()
    {
        var (game, player, state, _) = StartedSolo();

        Assert.True(game.Act(player, MOVE).Success);
        Assert.Equal(new Position(10, 9), state.Position);
    }

    [Fact]
    public void Rotate_ThenMove_StepsInNewDirection()
    {
        var (game, player, state, _) = StartedSolo();

        game.Act(player, new PlayerAction(ActionKind.Rotate, Direction.Right));
        game.Act(player, MOVE);

        Assert.Equal(Direction.Right, state.Facing);
        Assert.Equal(new Position(11, 10), state.Position);
    }

    [Fact]
    public void Move_WithinCooldown_IsRejectedUntilCooldownPasses()
    {
        var (game, player, state, time) = StartedSolo();
        game.Act(player, MOVE);

        time.Advance(Game.MOVE_COOLDOWN - TimeSpan.FromMilliseconds(1));
        Assert.Equal(GameError.OnCooldown, game.Act(player, MOVE).Error);
        Assert.Equal(new Position(10, 9), state.Position);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(game.Act(player, MOVE).Success);
        Assert.Equal(new Position(10, 8), state.Position);
    }

    [Fact]
    public void Move_OffTheBoard_IsRejected()
    {
        var (game, player, state, _) = StartedSolo();
        state.Position = new Position(0, 0);

        Assert.Equal(GameError.Blocked, game.Act(player, MOVE).Error);
        Assert.Equal(new Position(0, 0), state.Position);
    }

    [Fact]
    public void Move_IntoHole_IsRejected()
    {
        var (game, player, state, _) = StartedSolo();
        game.Board!.Set(new Position(10, 9), TileContent.Hole);

        Assert.Equal(GameError.Blocked, game.Act(player, MOVE).Error);
        Assert.Equal(MIDDLE, state.Position);
    }

    [Fact]
    public void Move_WhileFrozen_IsRejected()
    {
        var (game, player, state, time) = StartedSolo();
        state.FrozenUntil = time.Now + TimeSpan.FromSeconds(5);

        Assert.Equal(GameError.Frozen, game.Act(player, MOVE).Error);
        Assert.Equal(MIDDLE, state.Position);
    }

    [Fact]
    public void Move_OntoOtherPlayer_IsAllowed()
    {
        var time = new FakeTime();
        var game = new Game("1234", time);
        var p1 = game.Join("P1", "DEV_1").Player!;
        var p2 = game.Join("P2", "DEV_2").Player!;
        game.Start(new Random(1));
        var s1 = game.StateOf(p1.Id)!;
        (s1.Position, s1.Facing) = (MIDDLE, Direction.Up);
        game.StateOf(p2.Id)!.Position = new Position(10, 9);

        Assert.True(game.Act(p1, MOVE).Success);
        Assert.Equal(game.StateOf(p2.Id)!.Position, s1.Position);
    }

     [Fact]
    public void Move_Success_RaisesChangedOnce()
    {
        var (game, player, _, _) = StartedSolo();
        var raised = 0;
        game.Changed += () => raised++;

        game.Act(player, MOVE);
        game.Act(player, MOVE); // On cooldown: no event.

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Move_Rejected_DoesNotRaiseChanged()
    {
        var (game, player, state, _) = StartedSolo();
        state.Position = new Position(0, 0);
        var raised = false;
        game.Changed += () => raised = true;

        game.Act(player, MOVE);

        Assert.False(raised);
    }

    [Fact]
    public void Move_OntoPointMine_CostsPointsAndLeavesPlayerInHole()
    {
        var (game, player, state, _) = StartedSolo();
        var target = MIDDLE.Step(Direction.Up);
        game.Board!.Set(target, TileContent.PointMine);

        var hit = game.Act(player, MOVE).Mine!;

        Assert.Equal(TileContent.PointMine, hit.Mine);
        Assert.Equal(-Game.POINT_MINE_PENALTY, state.Score);
        Assert.Equal(1, state.PointMinesHit);
        Assert.Equal(target, state.Position);
        Assert.Equal(TileContent.Hole, game.Board[target]);
    }

    [Fact]
    public void Move_OntoFreezeMine_FreezesThenPlayerCanLeaveHole()
    {
        var (game, player, state, time) = StartedSolo();
        game.Board!.Set(MIDDLE.Step(Direction.Up), TileContent.FreezeMine);

        game.Act(player, MOVE);

        Assert.Equal(GameError.Frozen, game.Act(player, MOVE).Error);
        time.Advance(Game.FREEZE_DURATION);
        Assert.True(game.Act(player, MOVE).Success);
    }

    [Fact]
    public void Move_OntoScannedMine_IsSafe()
    {
        var (game, player, state, time) = StartedSolo();
        var target = MIDDLE.Step(Direction.Up);
        game.Board!.Set(target, TileContent.FreezeMine);
        game.Act(player, new PlayerAction(ActionKind.Scan));

        var result = game.Act(player, MOVE);

        Assert.True(result.Success);
        Assert.Null(result.Mine);
        Assert.Equal(target, state.Position);
        Assert.True(state.FrozenUntil <= time.Now);
        Assert.Equal(TileContent.DiscoveredFreezeMine, game.Board[target]);
    }
}