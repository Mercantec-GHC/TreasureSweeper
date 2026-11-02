using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests.Api;

public class LobbyApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> CreateGameAsync()
    {
        var response = await _client.PostAsync("/api/games", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("pin").GetString()!;
    }

    private Task<HttpResponseMessage> JoinAsync(string pin, string deviceId) =>
        _client.PostAsJsonAsync("/api/controller/join", new { pin, name = "Test", deviceId });

    [Fact]
    public async Task CreateGame_Returns201WithLocationAndWaitingPhase()
    {
        var response = await _client.PostAsync("/api/games", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var pin = body.GetProperty("pin").GetString();
        Assert.Equal($"/api/games/{pin}", response.Headers.Location!.ToString());
        Assert.Equal("Waiting", body.GetProperty("phase").GetString());
    }

    [Fact]
    public async Task ControllerJoin_Success_UsesFirmwareFieldNames()
    {
        var pin = await CreateGameAsync();

        var response = await JoinAsync(pin, "OPLA_A");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("playerId").GetString()));
        Assert.Equal(1, body.GetProperty("number").GetInt32());
        Assert.False(body.TryGetProperty("reconnected", out _));
        Assert.False(body.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ControllerJoin_SameDevice_ReturnsReconnectedTrue()
    {
        var pin = await CreateGameAsync();
        await JoinAsync(pin, "OPLA_A");

        var body = await (await JoinAsync(pin, "OPLA_A")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("reconnected").GetBoolean());
    }

    [Fact]
    public async Task ControllerJoin_UnknownPin_Returns404WithOkFalse()
    {
        var response = await JoinAsync("0000", "OPLA_A");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("ok").GetBoolean());
        Assert.True(body.TryGetProperty("error", out _));
        Assert.False(body.TryGetProperty("playerId", out _));
    }

    [Fact]
    public async Task ControllerJoin_NinthPlayer_Returns409()
    {
        var pin = await CreateGameAsync();
        for (var i = 1; i <= 8; i++)
            await JoinAsync(pin, $"OPLA_{i}");

        var response = await JoinAsync(pin, "OPLA_9");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ControllerJoin_MidRound_Returns409()
    {
        var pin = await CreateGameAsync();
        await JoinAsync(pin, "OPLA_1");
        await _client.PostAsync($"/api/games/{pin}/start", null);

        var response = await JoinAsync(pin, "OPLA_LATE");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Start_WithNoPlayers_Returns409ProblemDetails()
    {
        var pin = await CreateGameAsync();

        var response = await _client.PostAsync($"/api/games/{pin}/start", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GetGame_UnknownPin_Returns404()
    {
        var response = await _client.GetAsync("/api/games/0000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}