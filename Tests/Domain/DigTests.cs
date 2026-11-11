using Domain.Boards;
using Domain.Games;
using Tests.Support;

namespace Tests.Domain;

public class DigTests
{
    private static readonly Position MIDDLE = new(10, 10);
    private static readonly Position IN_FRONT = new(10, 9); // facing Up
    private static readonly PlayerAction DIG = new(ActionKind.Dig);

    private static (Game Game, Player[] Players, FakeTime Time) Started(int players = 1)
    {
        var time = new FakeTime();
        var game = new Game("1234", time);
        var joined = Enumerable.Range(1, players)
            .Select(i => game.Join($"P{i}", $"DEV_{i}").Player!)
            .ToArray();
        game.Start(new Random(1));

        for (var x = 0; x < game.Board!.Size; x++)
        for (var y = 0; y < game.Board.Size; y++)
            game.Board.Set(new(x, y), TileContent.Empty);

        // Spare treasure far away so digging a single treasure does not end the round.
        game.Board.Set(new(game.Board.Size - 1, game.Board.Size - 1), TileContent.Treasure);

        foreach (var p in joined)
        {
            var state = game.StateOf(p.Id)!;
            state.Position = MIDDLE;
            state.Facing = Direction.Up;
        }
        return (game, joined, time);
    }

    private static (Game Game, Player Player, PlayerState State, FakeTime Time) StartedSolo()
    {
        var (game, players, time) = Started();
        return (game, players[0], game.StateOf(players[0].Id)!, time);
    }

    [Fact]
    public void Dig_TreasureInFront_AwardsStartPointsAndLeavesHole()
    {
        var (game, player, state, _) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.Treasure);

        var dig = game.Act(player, DIG).Dig!;

        Assert.Equal(IN_FRONT, dig.Tile);
        Assert.Equal(TileContent.Treasure, dig.Found);
        Assert.Equal(Game.TREASURE_START_POINTS, dig.Points);
        Assert.Equal(Game.TREASURE_START_POINTS, state.Score);
        Assert.Equal(1, state.TreasuresFound);
        Assert.Equal(TileContent.Hole, game.Board[IN_FRONT]);
    }

    [Fact]
    public void Dig_LastTreasure_EndsRoundWithAllTreasuresFound()
    {
        var (game, player, _, _) = StartedSolo();
        game.Board!.Set(new(game.Board.Size - 1, game.Board.Size - 1), TileContent.Empty);
        game.Board.Set(IN_FRONT, TileContent.Treasure);

        game.Act(player, DIG);

        Assert.Equal(GamePhase.Ended, game.Phase);
        Assert.Equal(Shared.Games.RoundEndReason.AllTreasuresFound, game.EndReason);
        Assert.Equal(0, game.TreasuresLeft);
    }

    [Fact]
    public void Dig_TreasureWithOthersLeft_KeepsRoundPlaying()
    {
        var (game, player, _, _) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.Treasure);

        game.Act(player, DIG);

        Assert.Equal(GamePhase.Playing, game.Phase);
        Assert.Null(game.EndReason);
        Assert.Equal(1, game.TreasuresLeft);
    }

    [Fact]
    public void Dig_SecondTreasureOfRound_IsWorth100Less_EvenForAnotherPlayer()
    {
        var (game, players, _) = Started(players: 2);
        var (first, second) = (players[0], players[1]);
        game.StateOf(second.Id)!.Facing = Direction.Down;
        game.Board!.Set(IN_FRONT, TileContent.Treasure);
        game.Board.Set(new(10, 11), TileContent.Treasure);

        game.Act(first, DIG);
        var dig = game.Act(second, DIG).Dig!;

        Assert.Equal(Game.TREASURE_START_POINTS - 100, dig.Points);
        Assert.Equal(Game.TREASURE_START_POINTS - 100, game.StateOf(second.Id)!.Score);
    }

    [Fact]
    public void Dig_SameTreasureTwice_SecondPlayerGetsNothing()
    {
        var (game, players, _) = Started(players: 2);
        game.Board!.Set(IN_FRONT, TileContent.Treasure);

        game.Act(players[0], DIG);
        var dig = game.Act(players[1], DIG).Dig!;

        Assert.Equal(0, dig.Points);
        Assert.Equal(0, game.StateOf(players[1].Id)!.Score);
    }

    [Fact]
    public void Dig_ScannedTreasure_StillCounts()
    {
        var (game, player, state, _) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.Treasure);
        game.Act(player, new PlayerAction(ActionKind.Scan));
        Assert.Equal(TileContent.DiscoveredTreasure, game.Board[IN_FRONT]);

        game.Act(player, DIG);

        Assert.Equal(Game.TREASURE_START_POINTS, state.Score);
    }

    [Fact]
    public void Dig_EmptyTile_CreatesHoleAndStartsCooldown()
    {
        var (game, player, state, _) = StartedSolo();
        var changes = 0;
        game.Changed += () => changes++;

        var result = game.Act(player, DIG);

        Assert.True(result.Success);
        Assert.Equal(IN_FRONT, result.Dig!.Tile);
        Assert.Equal(TileContent.Empty, result.Dig.Found);
        Assert.Equal(0, result.Dig.Points);
        Assert.Equal(0, state.Score);
        Assert.Equal(TileContent.Hole, game.Board![IN_FRONT]);
        Assert.Equal(TileContent.Hole, game.Board[IN_FRONT].Visible());
        Assert.Equal(GameError.Blocked, game.Act(player, new PlayerAction(ActionKind.Move)).Error);
        Assert.Equal(GameError.OnCooldown, game.Act(player, DIG).Error);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(0, TileContent.MineClose0)]
    [InlineData(1, TileContent.MineClose1)]
    [InlineData(2, TileContent.MineClose2)]
    [InlineData(3, TileContent.MineClose3)]
    [InlineData(4, TileContent.MineClose4)]
    [InlineData(5, TileContent.MineClose5)]
    [InlineData(6, TileContent.MineClose6)]
    [InlineData(7, TileContent.MineClose7)]
    [InlineData(8, TileContent.MineClose8)]
    public void Dig_MineCloseTile_ReplacesMineCountWithHole(int adjacentMines, TileContent expectedTile)
    {
        var (game, player, state, _) = StartedSolo();
        var board = game.Board!;
        foreach (var neighbour in board.Neighbours(IN_FRONT).Take(adjacentMines))
            board.Set(neighbour, neighbour == state.Position ? TileContent.DiscoveredPointMine : TileContent.PointMine);

        var scan = game.Act(player, new PlayerAction(ActionKind.Scan));
        Assert.True(scan.Success);
        Assert.Equal(adjacentMines, scan.Scan!.AdjacentMines);
        Assert.Equal(expectedTile, board[IN_FRONT]);

        var dig = game.Act(player, DIG).Dig!;

        Assert.Equal(TileContent.Empty, dig.Found);
        Assert.Equal(0, dig.Points);
        Assert.Equal(0, state.Score);
        Assert.Equal(TileContent.Hole, board[IN_FRONT]);
        Assert.Equal(TileContent.Hole, board[IN_FRONT].Visible());
        Assert.Equal(GameError.Blocked, game.Act(player, new PlayerAction(ActionKind.Move)).Error);
    }

    [Fact]
    public void Dig_DifferentEmptyTiles_CreatesMultipleHoles()
    {
        var (game, player, _, time) = StartedSolo();
        var toRight = new Position(11, 10);

        Assert.True(game.Act(player, DIG).Success);
        Assert.True(game.Act(player, new PlayerAction(ActionKind.Rotate, Direction.Right)).Success);
        time.Advance(Game.DIG_COOLDOWN);
        Assert.True(game.Act(player, DIG).Success);

        Assert.Equal(TileContent.Hole, game.Board![IN_FRONT]);
        Assert.Equal(TileContent.Hole, game.Board[toRight]);
    }

    [Fact]
    public void Dig_OwnTile_IsNotDug()
    {
        var (game, player, state, _) = StartedSolo();
        game.Board!.Set(MIDDLE, TileContent.Treasure);

        game.Act(player, DIG);

        Assert.Equal(0, state.Score);
        Assert.Equal(TileContent.Treasure, game.Board[MIDDLE]);
    }

    // Mines are not handled yet; update this when mine scoring lands.
    [Fact]
    public void Dig_HiddenPointMine_CostsPointsAndLeavesHole()
    {
        var (game, player, state, _) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.PointMine);

        var dig = game.Act(player, DIG).Dig!;

        Assert.Equal(-Game.POINT_MINE_PENALTY, state.Score);
        Assert.Equal(1, state.PointMinesHit);
        Assert.Equal(TileContent.Hole, game.Board[IN_FRONT]);
    }

    [Fact]
    public void Dig_HiddenFreezeMine_FreezesPlayer()
    {
        var (game, player, state, time) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.FreezeMine);

        game.Act(player, DIG);

        Assert.Equal(0, state.Score);
        Assert.Equal(1, state.FreezeMinesHit);
        time.Advance(Game.FREEZE_DURATION - TimeSpan.FromMilliseconds(1));
        Assert.Equal(GameError.Frozen, game.Act(player, new PlayerAction(ActionKind.Rotate, Direction.Left)).Error);
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(game.Act(player, new PlayerAction(ActionKind.Rotate, Direction.Left)).Success);
    }

        [Theory]
    [InlineData(TileContent.PointMine)]
    [InlineData(TileContent.FreezeMine)]
    public void Dig_ScannedMine_DoesNothingAndMineStays(TileContent mine)
    {
        var (game, player, state, time) = StartedSolo();
        game.Board!.Set(IN_FRONT, mine);
        game.Act(player, new PlayerAction(ActionKind.Scan));
        var scanned = game.Board[IN_FRONT];

        var dig = game.Act(player, DIG).Dig!;

        Assert.False(dig.Triggered);
        Assert.Equal(0, state.Score);
        Assert.True(state.FrozenUntil <= time.Now);
        Assert.Equal(scanned, game.Board[IN_FRONT]);
    }

    [Fact]
    public void Dig_MineHole_StaysUntilFilled()
    {
        var (game, player, _, time) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.PointMine);
        game.Act(player, DIG);

        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(TileContent.Hole, game.Board[IN_FRONT]);
        Assert.Equal(GameError.Blocked, game.Act(player, new PlayerAction(ActionKind.Move)).Error);
    }

    [Fact]
    public void Dig_Hole_FillsItAndMakesItWalkable()
    {
        var (game, player, state, _) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.Hole);

        var dig = game.Act(player, DIG).Dig!;

        Assert.Equal(TileContent.Hole, dig.Found);
        Assert.Equal(0, dig.Points);
        Assert.Equal(TileContent.Empty, game.Board[IN_FRONT]);
        Assert.True(game.Act(player, new PlayerAction(ActionKind.Move)).Success);
        Assert.Equal(IN_FRONT, state.Position);
    }

    [Fact]
    public void Dig_SameTreasureTwice_SecondPlayerFillsTheHole()
    {
        var (game, players, _) = Started(players: 2);
        game.Board!.Set(IN_FRONT, TileContent.Treasure);

        game.Act(players[0], DIG);
        var dig = game.Act(players[1], DIG).Dig!;

        Assert.Equal(TileContent.Hole, dig.Found);
        Assert.Equal(0, game.StateOf(players[1].Id)!.Score);
        Assert.Equal(TileContent.Empty, game.Board[IN_FRONT]);
    }

    [Fact]
    public void Dig_FacingEdge_IsBlockedAndStartsNoCooldown()
    {
        var (game, player, state, _) = StartedSolo();
        state.Position = new(0, 0);

        Assert.Equal(GameError.Blocked, game.Act(player, DIG).Error);

        state.Facing = Direction.Right;
        Assert.True(game.Act(player, DIG).Success);
    }

    [Fact]
    public void Dig_Twice_SecondIsOnCooldownForOneSecond()
    {
        var (game, player, _, time) = StartedSolo();
        Assert.True(game.Act(player, DIG).Success);
        Assert.Equal(TileContent.Hole, game.Board![IN_FRONT]);

        time.Advance(Game.DIG_COOLDOWN - TimeSpan.FromMilliseconds(1));
        Assert.Equal(GameError.OnCooldown, game.Act(player, DIG).Error);
        Assert.Equal(TileContent.Hole, game.Board[IN_FRONT]);

        time.Advance(TimeSpan.FromMilliseconds(1));
        var fill = game.Act(player, DIG);
        Assert.True(fill.Success);
        Assert.Equal(TileContent.Hole, fill.Dig!.Found);
        Assert.Equal(TileContent.Empty, game.Board[IN_FRONT]);

        time.Advance(Game.DIG_COOLDOWN);
        Assert.True(game.Act(player, DIG).Success);
        Assert.Equal(TileContent.Hole, game.Board[IN_FRONT]);
    }

    [Fact]
    public void Dig_CooldownDoesNotBlockScanOrMoveInAnotherDirection()
    {
        var (game, player, _, _) = StartedSolo();
        Assert.True(game.Act(player, DIG).Success);

        Assert.True(game.Act(player, new PlayerAction(ActionKind.Scan)).Success);
        Assert.Equal(GameError.Blocked, game.Act(player, new PlayerAction(ActionKind.Move)).Error);
        Assert.True(game.Act(player, new PlayerAction(ActionKind.Rotate, Direction.Right)).Success);
        Assert.True(game.Act(player, new PlayerAction(ActionKind.Move)).Success);
    }

    [Fact]
    public void Dig_WhileFrozen_IsRejectedAndTreasureStays()
    {
        var (game, player, state, time) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.Treasure);
        state.FrozenUntil = time.Now + TimeSpan.FromSeconds(5);

        Assert.Equal(GameError.Frozen, game.Act(player, DIG).Error);
        Assert.Equal(TileContent.Treasure, game.Board[IN_FRONT]);
    }

    [Fact]
    public void Dig_BeforeRound_IsRejected()
    {
        var game = new Game("1234", new FakeTime());
        var player = game.Join("P1", "DEV_1").Player!;

        Assert.Equal(GameError.RoundNotPlaying, game.Act(player, DIG).Error);
    }

    [Fact]
    public void NewRound_FirstTreasureIsWorthStartPointsAgain()
    {
        var (game, player, _, time) = StartedSolo();
        game.Board!.Set(IN_FRONT, TileContent.Treasure);
        game.Act(player, DIG);
        game.End();
        game.Reset();
        game.Start(new Random(1));

        var state = game.StateOf(player.Id)!;
        var tile = state.Position.Step(state.Facing);
        game.Board!.Set(tile, TileContent.Treasure);
        time.Advance(Game.DIG_COOLDOWN);

        Assert.Equal(Game.TREASURE_START_POINTS, game.Act(player, DIG).Dig!.Points);
    }
}