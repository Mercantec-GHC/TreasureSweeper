using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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
    }
}