using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Boards;
using Domain.Games;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Api;

public class ControllerActionApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string Pin, string PlayerId)> JoinedAsync()
    {
        var created = await (await _client.PostAsync("/api/games", null)).Content.ReadFromJsonAsync<JsonElement>();
        var pin = created.GetProperty("pin").GetString()!;
        var joined = await (await _client.PostAsJsonAsync("/api/controller/join", new { pin, name = "Test", deviceId = "OPLA_A" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return (pin, joined.GetProperty("playerId").GetString()!);
    }

        private async Task<(string Pin, string PlayerId)> StartedAsync()
    {
        var (pin, playerId) = await JoinedAsync();
        await _client.PostAsync($"/api/games/{pin}/start", null);
        return (pin, playerId);
    }

    private (Game Game, PlayerState State, Position InFront) PrepareInFront(string pin, string playerId, TileContent inFront)
    {
        var game = GameFor(pin);
        var state = game.StateOf(playerId)!;
        var tile = state.Position.Step(state.Facing);
        foreach (var n in game.Board!.Neighbours(tile)) game.Board.Set(n, TileContent.Empty);
        game.Board.Set(tile, inFront);
        return (game, state, tile);
    }

    private Game GameFor(string pin) =>
        factory.Services.GetRequiredService<GameRegistry>().Find(pin)!;
    
    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private Task<HttpResponseMessage> ActionAsync(string pin, string playerId, string action, string? direction = null, string deviceId = "OPLA_A") =>
        _client.PostAsJsonAsync("/api/controller/action",
            new { pin, playerId, deviceId, action, @params = new { direction } });

    private async Task<JsonElement> PlayerAsync(string pin) =>
        (await (await _client.GetAsync($"/api/games/{pin}")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("players")[0];

    [Theory]
    [InlineData("move", null)]
    [InlineData("move", "UP")]
    [InlineData("rotate", "LEFT")]
    [InlineData("rotate", "right")]
    [InlineData("scan", null)]
    [InlineData("dig", null)]
    public async Task DuringRound_Returns200OkTrue(string action, string? direction)
    {
        var (pin, playerId) = await JoinedAsync();
        await _client.PostAsync($"/api/games/{pin}/start", null);

        var response = await ActionAsync(pin, playerId, action, direction);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());
    }

    [Theory]
    [InlineData("fly", null)]
    [InlineData("rotate", null)]
    [InlineData("rotate", "UP")]
    [InlineData("rotate", "DOWN")]
    public async Task Invalid_Returns400(string action, string? direction)
    {
        var (pin, playerId) = await JoinedAsync();

        var response = await ActionAsync(pin, playerId, action, direction);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BeforeRound_Returns200OkFalseWithMessage()
    {
        var (pin, playerId) = await JoinedAsync();

        var response = await ActionAsync(pin, playerId, "dig");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.True(body.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task WrongDevice_Returns403()
    {
        var (pin, playerId) = await JoinedAsync();

        var response = await ActionAsync(pin, playerId, "scan", deviceId: "OPLA_B");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPin_Returns404()
    {
        var response = await ActionAsync("0000", "x", "scan");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Move_TwiceImmediately_SecondIsSilentlyRejected()
    {
        var (pin, playerId) = await StartedAsync();
        await ActionAsync(pin, playerId, "rotate", "LEFT");
        await ActionAsync(pin, playerId, "move");

        var response = await ActionAsync(pin, playerId, "move");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.True(body.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task Move_UpdatesPositionInGameResponse()
    {
        var (pin, playerId) = await StartedAsync();
        var before = await PlayerAsync(pin);
        var (x, y) = (before.GetProperty("position").GetProperty("x").GetInt32(),
                      before.GetProperty("position").GetProperty("y").GetInt32());

        await ActionAsync(pin, playerId, "move");

        var after = (await PlayerAsync(pin)).GetProperty("position");
        var (expectedX, expectedY) = before.GetProperty("facing").GetString() switch
        {
            "Up" => (x, y - 1),
            "Down" => (x, y + 1),
            "Left" => (x - 1, y),
            _ => (x + 1, y),
        };
        Assert.Equal(expectedX, after.GetProperty("x").GetInt32());
        Assert.Equal(expectedY, after.GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task Lobby_PlayersHaveNoPosition()
    {
        var created = await (await _client.PostAsync("/api/games", null)).Content.ReadFromJsonAsync<JsonElement>();
        var pin = created.GetProperty("pin").GetString()!;
        await _client.PostAsJsonAsync("/api/controller/join", new { pin, name = "Test", deviceId = "OPLA_A" });

        var player = await PlayerAsync(pin);

        Assert.False(player.TryGetProperty("position", out _));
        Assert.False(player.TryGetProperty("view", out _));
        Assert.False(player.TryGetProperty("score", out _));
    }

    [Fact]
    public async Task Scan_MineInFront_ReturnsRedLight()
    {
        var (pin, playerId) = await StartedAsync();
        var game = GameFor(pin);
        var state = game.StateOf(playerId)!;
        game.Board!.Set(state.Position.Step(state.Facing), TileContent.PointMine);

        var body = await (await ActionAsync(pin, playerId, "scan")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("red", body.GetProperty("light").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Scan_SafeTile_ReturnsLightOffAndCount(int mines)
    {
        var (pin, playerId) = await StartedAsync();
        var (game, state, tile) = PrepareInFront(pin, playerId, TileContent.Empty);
        foreach (var n in game.Board!.Neighbours(tile).Where(n => n != state.Position).Take(mines))
            game.Board.Set(n, TileContent.FreezeMine);

        var body = await BodyAsync(await ActionAsync(pin, playerId, "scan"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("off", body.GetProperty("light").GetString());
        Assert.Equal(mines, body.GetProperty("count").GetInt32()); // 0 must be sent, not omitted
    }

    [Theory]
    [InlineData("move", null)]
    [InlineData("rotate", "LEFT")]
    public async Task NonScanActions_HaveNoLightOrCount(string action, string? direction)
    {
        var (pin, playerId) = await StartedAsync();

        var body = await BodyAsync(await ActionAsync(pin, playerId, action, direction));

        Assert.False(body.TryGetProperty("light", out _));
        Assert.False(body.TryGetProperty("count", out _));
    }

    [Fact]
    public async Task Scan_Twice_SecondIsRejectedWithoutLight()
    {
        var (pin, playerId) = await StartedAsync();
        await ActionAsync(pin, playerId, "scan");

        var response = await ActionAsync(pin, playerId, "scan");
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("light", out _));
    }

    [Fact]
    public async Task Scan_ShowsCountInPlayerView()
    {
        var (pin, playerId) = await StartedAsync();
        var (game, state, tile) = PrepareInFront(pin, playerId, TileContent.Empty);
        game.Board!.Set(game.Board.Neighbours(tile).First(n => n != state.Position), TileContent.PointMine);

        await ActionAsync(pin, playerId, "scan");

        var tiles = (await PlayerAsync(pin)).GetProperty("view");
        Assert.Equal(25, tiles.GetArrayLength());
        Assert.Equal((int)TileContent.MineClose1, tiles[7].GetInt32());  // tile in front
        Assert.Equal((int)TileContent.Empty, tiles[12].GetInt32());      // own tile, not scanned
    }

    [Fact]
    public async Task PlayerView_ScannedTileShowsContent_UnscannedTileDoesNot()
    {
        var (pin, playerId) = await StartedAsync();
        var state = GameFor(pin).StateOf(playerId)!;
        state.Position = new(10, 10);
        state.Facing = Direction.Up;
        var (game, _, _) = PrepareInFront(pin, playerId, TileContent.PointMine); // (10,9)
        game.Board!.Set(new(10, 8), TileContent.Treasure);                     // not scanned
        game.Board.Set(new(11, 8), TileContent.FreezeMine);                    // not scanned, next to (10,9)

        await ActionAsync(pin, playerId, "scan");

        var view = (await PlayerAsync(pin)).GetProperty("view");
        Assert.Equal((int)TileContent.DiscoveredPointMine, view[7].GetInt32()); // content, no count
        Assert.Equal((int)TileContent.Empty, view[2].GetInt32());                   // treasure stays hidden
        Assert.Equal((int)TileContent.Empty, view[3].GetInt32());                   // mine stays hidden
    }

    [Fact]
    public async Task PlayerView_InCornerFacingUp_MarksTilesOffTheBoardAsOutside()
    {
        var (pin, playerId) = await StartedAsync();
        var state = GameFor(pin).StateOf(playerId)!;
        state.Position = new(0, 0);
        state.Facing = Direction.Up;

        var tiles = (await PlayerAsync(pin)).GetProperty("view");

        for (var row = 0; row < 5; row++)
        for (var col = 0; col < 5; col++)
            Assert.Equal(row < 2 || col < 2, tiles[row * 5 + col].ValueKind == JsonValueKind.Null);
    }

        [Fact]
    public async Task Dig_TreasureInFront_ReturnsYellowLight()
    {
        var (pin, playerId) = await StartedAsync();
        PrepareInFront(pin, playerId, TileContent.Treasure);

        var body = await BodyAsync(await ActionAsync(pin, playerId, "dig"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("yellow", body.GetProperty("light").GetString());
        Assert.False(body.TryGetProperty("count", out _));
    }

    [Fact]
    public async Task Dig_EmptyTile_ReturnsLightOff()
    {
        var (pin, playerId) = await StartedAsync();
        PrepareInFront(pin, playerId, TileContent.Empty);

        var body = await BodyAsync(await ActionAsync(pin, playerId, "dig"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("off", body.GetProperty("light").GetString());
    }

    [Fact]
    public async Task Dig_Treasure_UpdatesScoreInGameResponse()
    {
        var (pin, playerId) = await StartedAsync();
        Assert.Equal(0, (await PlayerAsync(pin)).GetProperty("score").GetInt32());
        PrepareInFront(pin, playerId, TileContent.Treasure);

        await ActionAsync(pin, playerId, "dig");

        Assert.Equal(Game.TREASURE_START_POINTS, (await PlayerAsync(pin)).GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task Move_OntoPointMine_ReturnsPurpleLight()
    {
        var (pin, playerId) = await StartedAsync();
        PrepareInFront(pin, playerId, TileContent.PointMine);

        var body = await BodyAsync(await ActionAsync(pin, playerId, "move"));

        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("purple", body.GetProperty("light").GetString());
    }
}