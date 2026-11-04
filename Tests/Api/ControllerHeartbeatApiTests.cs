using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests.Api;

public class ControllerHeartbeatApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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

    private Task<HttpResponseMessage> HeartbeatAsync(string pin, string playerId, string deviceId) =>
        _client.PostAsJsonAsync("/api/controller/heartbeat", new { pin, playerId, deviceId });

    [Fact]
    public async Task KnownController_Returns200OkTrue()
    {
        var (pin, playerId) = await JoinedAsync();

        var response = await HeartbeatAsync(pin, playerId, "OPLA_A");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task UnknownPlayer_Returns403()
    {
        var (pin, _) = await JoinedAsync();

        var response = await HeartbeatAsync(pin, "nope", "OPLA_A");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WrongDevice_Returns403()
    {
        var (pin, playerId) = await JoinedAsync();

        var response = await HeartbeatAsync(pin, playerId, "OPLA_B");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPin_Returns404()
    {
        var response = await HeartbeatAsync("0000", "x", "OPLA_A");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}