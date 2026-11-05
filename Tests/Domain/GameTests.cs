using Domain.Games;

namespace Tests.Domain;

public class GameTests
{
    private static Game GameWithPlayers(int count)
    {
        var game = new Game("1234");
        for (var i = 1; i <= count; i++)
            Assert.True(game.Join($"P{i}", $"DEV_{i}").Success);
        return game;
    }

        [Fact]
    public void Join_FirstPlayer_GetsNumberOneAndItsIdentity()
    {
        var game = new Game("1234");

        var result = game.Join("Mikkel", "DEV_1");

        Assert.True(result.Success);
        Assert.False(result.Reconnected);
        var expected = PlayerIdentity.ForNumber(1);
        Assert.Equal(1, result.Player!.Number);
        Assert.Equal(expected.Color, result.Player.Color);
        Assert.Equal(expected.Symbol, result.Player.Symbol);
    }

    [Fact]
    public void Join_EightPlayers_GetNumbersOneToEight()
    {
        var game = GameWithPlayers(8);

        Assert.Equal(Enumerable.Range(1, 8), game.Players.Select(p => p.Number).Order());
    }

    [Fact]
    public void Join_NinthPlayer_FailsWithGameFull()
    {
        var game = GameWithPlayers(8);

        var result = game.Join("P9", "DEV_9");

        Assert.Equal(GameError.GameFull, result.Error);
        Assert.Equal(8, game.Players.Count);
    }

    [Fact]
    public void Join_SameDeviceTwice_ReturnsSamePlayerAsReconnect()
    {
        var game = new Game("1234");
        var first = game.Join("Mikkel", "DEV_1");

        var second = game.Join("Mikkel", "DEV_1");

        Assert.True(second.Reconnected);
        Assert.Equal(first.Player, second.Player);
        Assert.Single(game.Players);
    }

    [Fact]
    public void Join_KnownDeviceWhenFull_StillReconnects()
    {
        var game = GameWithPlayers(8);

        var result = game.Join("P3", "DEV_3");

        Assert.True(result.Success);
        Assert.True(result.Reconnected);
        Assert.Equal(3, result.Player!.Number);
    }

    [Fact]
    public void Join_WithoutDeviceId_AlwaysCreatesNewPlayer()
    {
        var game = new Game("1234");

        game.Join("A", null);
        game.Join("A", null);

        Assert.Equal(2, game.Players.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Join_BlankName_GetsArduinoFallback(string? name)
    {
        var game = new Game("1234");

        var result = game.Join(name, "DEV_1");

        Assert.Equal("Arduino 1", result.Player!.Name);
    }

    [Fact]
    public void Join_NameWithSpaces_IsTrimmed()
    {
        var result = new Game("1234").Join("  Mikkel  ", "DEV_1");

        Assert.Equal("Mikkel", result.Player!.Name);
    }

    [Fact]
    public void Join_NewDeviceWhilePlaying_FailsWithRoundInProgress()
    {
        var game = GameWithPlayers(1);
        game.Start();

        var result = game.Join("Late", "DEV_LATE");

        Assert.Equal(GameError.RoundInProgress, result.Error);
        Assert.Single(game.Players);
    }

    [Fact]
    public void Join_KnownDeviceWhilePlaying_Reconnects()
    {
        var game = GameWithPlayers(1);
        game.Start();

        var result = game.Join("P1", "DEV_1");

        Assert.True(result.Reconnected);
    }

    [Fact]
    public void Join_BetweenRounds_IsAllowed()
    {
        var game = GameWithPlayers(1);
        game.Start();
        game.End();

        var result = game.Join("New", "DEV_NEW");

        Assert.True(result.Success);
        Assert.Equal(2, result.Player!.Number);
    }

    // ---------- Phases ----------

    [Fact]
    public void NewGame_IsWaiting()
    {
        Assert.Equal(GamePhase.Waiting, new Game("1234").Phase);
    }

    [Fact]
    public void Start_WithNoPlayers_FailsAndStaysWaiting()
    {
        var game = new Game("1234");

        var result = game.Start();

        Assert.Equal(GameError.NoPlayers, result.Error);
        Assert.Equal(GamePhase.Waiting, game.Phase);
    }

    [Fact]
    public void Start_WithOnePlayer_GoesToPlaying()
    {
        var game = GameWithPlayers(1);

        Assert.True(game.Start().Success);
        Assert.Equal(GamePhase.Playing, game.Phase);
    }

    [Fact]
    public void Start_WhilePlaying_Fails()
    {
        var game = GameWithPlayers(1);
        game.Start();

        Assert.Equal(GameError.RoundNotWaiting, game.Start().Error);
    }

    [Fact]
    public void Start_AfterEnded_FailsUntilReset()
    {
        var game = GameWithPlayers(1);
        game.Start();
        game.End();

        Assert.Equal(GameError.RoundNotWaiting, game.Start().Error);
    }

    [Fact]
    public void End_WhenWaiting_Fails()
    {
        Assert.Equal(GameError.RoundNotPlaying, GameWithPlayers(1).End().Error);
    }

    [Fact]
    public void Reset_WhilePlaying_Fails()
    {
        var game = GameWithPlayers(1);
        game.Start();

        Assert.Equal(GameError.RoundInProgress, game.Reset().Error);
        Assert.Equal(GamePhase.Playing, game.Phase);
    }

    [Fact]
    public void Reset_AfterEnded_KeepsPlayersAndIdentities()
    {
        var game = GameWithPlayers(3);
        var before = game.Players.OrderBy(p => p.Number).ToList();
        game.Start();
        game.End();

        game.Reset();

        Assert.Equal(GamePhase.Waiting, game.Phase);
        Assert.Equal(before, game.Players.OrderBy(p => p.Number).ToList());
    }

    [Fact]
    public void FullCycle_WaitingPlayingEndedWaitingPlaying_Works()
    {
        var game = GameWithPlayers(2);

        Assert.True(game.Start().Success);
        Assert.True(game.End().Success);
        Assert.True(game.Reset().Success);
        Assert.True(game.Start().Success);
        Assert.Equal(GamePhase.Playing, game.Phase);
    }

    [Fact]
    public void Start_NewRound_GeneratesNewBoard()
    {
        var game = GameWithPlayers(2);
        game.Start(new Random(1));
        var first = game.Board;
        game.End();
        game.Reset();

        game.Start(new Random(2));

        Assert.NotSame(first, game.Board);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Start_WithPlayers_CreatesBoardWithSpawnPerPlayer(int players)
    {
        var game = GameWithPlayers(players);
        game.Start();
        Assert.Equal(players, game.Board?.Spawns.Count);
    }

    [Fact]
    public void Start_NoPlayers_CreatesNoBoard()
    {
        var game = GameWithPlayers(0);
        game.Start();

        Assert.Null(game.Board);
    }
    

    [Fact]
    public void Reset_AfterRound_ClearsBoard()
    {
        var game = GameWithPlayers(2);
        game.Start();
        game.End();

        game.Reset();

        Assert.Null(game.Board);
    }
}