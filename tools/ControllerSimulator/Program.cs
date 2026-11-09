// Treasure Sweeper - Controller Simulator
// Pretends to be one or more Arduino Oplà controllers so the game can be tested
// without hardware (issue #51). It speaks the same HTTP/JSON contract as the
// firmware (see docs/controller-api.md).
//
// Usage:
//   dotnet run --project tools/ControllerSimulator -- [options]
//
// Options:
//   --url <base>       Backend base URL (default http://localhost:5288)
//   --pin <pin>        Join an existing game. If omitted, a new game is created.
//   --players <n>      Number of simulated controllers to join (default 2)
//   --prefix <name>    Player name prefix (default "Sim")
//
// Keys (while running):
//   1-8        select the active controller
//   W / Up     move forward          S  scan (tile in front)
//   A / Left   rotate left           F  dig  (tile in front)
//   D / Right  rotate right
//   Enter      start the round (host)     R  reset to lobby (host)
//   Tab        print the status table     Q / Esc  quit

using System.Net.Http.Json;
using System.Text.Json;

// ---- parse arguments -------------------------------------------------------
string baseUrl = GetOption("--url") ?? "http://localhost:5288";
string? pin = GetOption("--pin");
int playerCount = int.TryParse(GetOption("--players"), out var n) ? Math.Clamp(n, 1, 8) : 2;
string prefix = GetOption("--prefix") ?? "Sim";

var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };
var consoleLock = new object();
bool heartbeatMissingReported = false;

Log($"Controller Simulator -> {baseUrl}");

// ---- create or find the game ----------------------------------------------
if (pin is null)
{
    pin = await CreateGameAsync();
    if (pin is null) { Log("Could not create a game. Is the backend running?"); return; }
    Log($"Created game with PIN {pin}");
}
else
{
    Log($"Using existing game PIN {pin}");
}

// ---- join the simulated controllers ---------------------------------------
var players = new List<Player>();
for (var i = 1; i <= playerCount; i++)
{
    var deviceId = $"SIM_{i:00}";
    var name = $"{prefix}{i}";
    var p = await JoinAsync(deviceId, name);
    if (p is not null) { players.Add(p); Log($"Joined #{p.Number} {p.Name} (device {deviceId}, id {Short(p.PlayerId)})"); }
    else Log($"Join failed for {deviceId}");
}
if (players.Count == 0) { Log("No controllers joined; nothing to do."); return; }

int active = 0;
PrintControls();
PrintStatusTable();

// ---- background heartbeat (every 2 s, like the firmware) -------------------
using var cts = new CancellationTokenSource();
var heartbeatLoop = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        foreach (var p in players) await HeartbeatAsync(p);
        try { await Task.Delay(2000, cts.Token); } catch (TaskCanceledException) { return; }
    }
});

// ---- key loop --------------------------------------------------------------
Log("Ready. Open the game screen in a browser to watch the board.");
while (true)
{
    var key = Console.ReadKey(intercept: true);
    var p = players[active];
    switch (key.Key)
    {
        case ConsoleKey.D1 or ConsoleKey.D2 or ConsoleKey.D3 or ConsoleKey.D4
          or ConsoleKey.D5 or ConsoleKey.D6 or ConsoleKey.D7 or ConsoleKey.D8:
            var sel = key.Key - ConsoleKey.D1;
            if (sel < players.Count) { active = sel; Log($"Active controller: #{players[active].Number} {players[active].Name}"); }
            break;

        case ConsoleKey.W or ConsoleKey.UpArrow:    await ActionAsync(p, "move", "UP"); break;
        case ConsoleKey.A or ConsoleKey.LeftArrow:  await ActionAsync(p, "rotate", "LEFT"); break;
        case ConsoleKey.D or ConsoleKey.RightArrow: await ActionAsync(p, "rotate", "RIGHT"); break;
        case ConsoleKey.S:                          await ActionAsync(p, "scan"); break;
        case ConsoleKey.F:                          await ActionAsync(p, "dig"); break;

        case ConsoleKey.Enter: await HostAsync("start"); break;
        case ConsoleKey.R:     await HostAsync("reset"); break;
        case ConsoleKey.Tab:   PrintStatusTable(); break;

        case ConsoleKey.Q or ConsoleKey.Escape:
            cts.Cancel();
            Log("Bye.");
            return;
    }
}

// ---- HTTP calls ------------------------------------------------------------

async Task<string?> CreateGameAsync()
{
    try
    {
        // Send an explicit empty object (works around the empty-body create bug, #70).
        var res = await http.PostAsJsonAsync("/api/games", new { });
        if (!res.IsSuccessStatusCode) { Log($"Create game -> {(int)res.StatusCode}"); return null; }
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("pin", out var v) ? v.GetString() : null;
    }
    catch (Exception ex) { Log($"Create game error: {ex.Message}"); return null; }
}

async Task<Player?> JoinAsync(string deviceId, string name)
{
    try
    {
        var res = await http.PostAsJsonAsync("/api/controller/join", new { pin, name, deviceId });
        var body = await res.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;
        if (!res.IsSuccessStatusCode || !GetBool(root, "ok"))
        {
            Log($"Join {deviceId} -> {(int)res.StatusCode} {GetString(root, "error")}");
            return null;
        }
        return new Player
        {
            DeviceId = deviceId,
            Name = GetString(root, "name") ?? name,
            Number = root.TryGetProperty("number", out var num) ? num.GetInt32() : 0,
            PlayerId = GetString(root, "playerId") ?? ""
        };
    }
    catch (Exception ex) { Log($"Join {deviceId} error: {ex.Message}"); return null; }
}

async Task HeartbeatAsync(Player p)
{
    try
    {
        var res = await http.PostAsJsonAsync("/api/controller/heartbeat", new { pin, deviceId = p.DeviceId, playerId = p.PlayerId });
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!heartbeatMissingReported) { heartbeatMissingReported = true; Log("(heartbeat endpoint not implemented yet - #10/#21)"); }
            return;
        }
        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var phaseBefore = p.Phase; var scoreBefore = p.Score;
        p.Phase = GetString(root, "phase") ?? p.Phase;
        if (root.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number) p.Score = s.GetInt32();
        if (root.TryGetProperty("light", out var l)) p.Light = l.GetString() ?? p.Light;
        if (root.TryGetProperty("frozenMilliseconds", out var fz) && fz.ValueKind == JsonValueKind.Number) p.FrozenMs = fz.GetInt32();
        if (p.Phase != phaseBefore || p.Score != scoreBefore)
            Log($"[#{p.Number} {p.Name}] phase={p.Phase} score={p.Score} light={p.Light}");
    }
    catch { /* transient; ignore */ }
}

async Task ActionAsync(Player p, string action, string? direction = null)
{
    try
    {
        object payload = direction is null
            ? new { pin, deviceId = p.DeviceId, playerId = p.PlayerId, action }
            : new { pin, deviceId = p.DeviceId, playerId = p.PlayerId, action, @params = new { direction } };
        var res = await http.PostAsJsonAsync("/api/controller/action", payload);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Log($"[#{p.Number}] {action}{Dir(direction)} -> endpoint not implemented yet (#10/#21)");
            return;
        }
        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        if (!GetBool(root, "ok")) { Log($"[#{p.Number}] {action}{Dir(direction)} refused: {GetString(root, "error")}"); return; }

        if (root.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number) p.Score = s.GetInt32();
        if (root.TryGetProperty("light", out var l)) p.Light = l.GetString() ?? p.Light;
        var result = GetString(root, "result") ?? action;
        var extra = "";
        if (root.TryGetProperty("scan", out var scan))
        {
            var mine = GetBool(scan, "mine");
            var around = scan.TryGetProperty("around", out var a) ? a.GetInt32() : 0;
            var near = GetBool(scan, "treasureNear");
            extra = $" scan[mine={mine} around={around} treasureNear={near}]";
        }
        Log($"[#{p.Number} {p.Name}] {action}{Dir(direction)} -> {result} score={p.Score} light={p.Light}{extra}");
    }
    catch (Exception ex) { Log($"[#{p.Number}] {action} error: {ex.Message}"); }
}

async Task HostAsync(string what)
{
    try
    {
        var res = await http.PostAsync($"/api/games/{pin}/{what}", null);
        Log($"Host {what} -> {(int)res.StatusCode}");
    }
    catch (Exception ex) { Log($"Host {what} error: {ex.Message}"); }
}

// ---- helpers ---------------------------------------------------------------

string? GetOption(string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

void PrintControls()
{
    lock (consoleLock)
    {
        Console.WriteLine();
        Console.WriteLine("  Controls:  [1-8] select   [W/Up] move   [A/Left] rotate L   [D/Right] rotate R");
        Console.WriteLine("             [S] scan   [F] dig   [Enter] start round   [R] reset   [Tab] status   [Q] quit");
        Console.WriteLine();
    }
}

void PrintStatusTable()
{
    lock (consoleLock)
    {
        Console.WriteLine();
        Console.WriteLine($"  PIN {pin}   ({players.Count} controllers)");
        Console.WriteLine("  #  Name        Score  Phase     Light    Frozen");
        foreach (var p in players)
            Console.WriteLine($"  {p.Number,-2} {p.Name,-11} {p.Score,5}  {p.Phase,-8}  {p.Light,-7}  {(p.FrozenMs > 0 ? p.FrozenMs + "ms" : "-")}");
        Console.WriteLine();
    }
}

void Log(string msg)
{
    lock (consoleLock) Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
}

static string Short(string id) => id.Length > 8 ? id[..8] : id;
static string Dir(string? d) => d is null ? "" : $" {d}";
static bool GetBool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
static string? GetString(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;

sealed class Player
{
    public required string DeviceId { get; init; }
    public required string Name { get; set; }
    public int Number { get; set; }
    public required string PlayerId { get; set; }
    public int Score { get; set; }
    public string Phase { get; set; } = "?";
    public string Light { get; set; } = "off";
    public int FrozenMs { get; set; }
}
