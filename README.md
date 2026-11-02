# Treasure Sweeper

A multiplayer "treasure hunt meets Minesweeper" party game for up to 8 players. Each player steers with an Arduino Oplà controller, and the shared game screen shows the board, scores and leaderboard.

It is a single ASP.NET Core application on **.NET 10** that serves a **Blazor Server** UI and the HTTP endpoints the Oplà controllers talk to. Both run in the same process and share the same live game state.

## Tech Stack

| Layer      | Project        | Technology / purpose                                                          |
| ---------- | -------------- | ----------------------------------------------------------------------------- |
| Web host   | `src/Blazor`   | Blazor Server UI **and** minimal-API endpoints for the Oplà (one process)     |
| Domain     | `src/Domain`   | Game rules: lobby, players, board, treasure/mine placement. No web code       |
| Contracts  | `src/Shared`   | Plain DTO records for the Oplà JSON contract. No dependencies                 |
| Controller | `Arduino`      | Firmware for the Arduino Oplà controller                                      |
| Tests      | `Tests`        | xUnit unit tests (Domain) and integration tests (HTTP endpoints)              |
| Platform   | —              | .NET 10 (`net10.0`), C#                                                       |
| Solution   | `Treasure-sweeper.slnx` | Visual Studio solution (XML format)                                  |

## Architecture

```
 Arduino Oplà ──HTTP/JSON──►  ┌───────────────────────────────────────────┐
 (controller)                 │  src/Blazor  (one ASP.NET Core process)   │
                              │                                           │
 Browser (game screen) ◄─SignalR─►  Pages/ (Razor components)             │
                              │        │                                  │
                              │        ▼                                  │
                              │   GameRegistry (singleton, in memory)  ◄──┼── Endpoints/ + Mapping/
                              │        │                                  │   (Oplà HTTP API)
                              └────────┼──────────────────────────────────┘
                                       ▼
                                  src/Domain   (rules, no web code)
```

Project references:

```
Blazor ──► Domain
   └─────► Shared
Tests  ──► Blazor, Domain, Shared
```

### Project structure

```
TreasureSweeper/
├── Treasure-sweeper.slnx     # Solution
├── run.ps1                   # Starts the app from the CLI
├── Arduino/                  # Controller firmware
├── Tests/                    # xUnit tests — references Blazor (host), Domain and Shared
└── src/
    ├── Blazor/               # The one web host
    │   ├── Program.cs        # Wires up Blazor Server, GameRegistry and the endpoints
    │   ├── Pages/, Layout/   # UI (Razor components)
    │   ├── Endpoints/        # HTTP endpoints for the Oplà (namespace API.Endpoints)
    │   ├── Mapping/          # Domain → Shared DTO mapping (namespace API.Mapping)
    │   └── API.http          # Manual requests for every endpoint
    ├── Domain/               # Game logic — no project references
    └── Shared/               # DTOs — no project references, no ASP.NET dependencies
```

The `API.*` namespaces are left over from when the endpoints had their own project. They can be renamed later without changing behaviour.

## Why Blazor Server and not WebAssembly (WASM)

The game screen is a **passive display**. The players use the Oplà, not the browser. The one thing the UI has to do fast is show the current game state, and that state is already on the server.

| | Blazor Server (chosen) | Blazor WASM (rejected) |
| --- | --- | --- |
| Reading game state | Directly from `GameRegistry` in memory | Over HTTP through DTOs and an API |
| Live updates | Pushed over the built-in SignalR connection | Needs its own SignalR hub and client, or polling |
| Start-up | Instant, nothing to download | Downloads the .NET runtime on first load |
| Hosting | One project, one container | A separate static host plus CORS |
| Hidden board | Code and data never leave the server | Needs a rule to keep Domain out of the browser |

**Latency.** The path that matters for responsiveness is *Oplà touch → backend → screen*. It is the same in both options: an HTTP request from the controller, the game logic, then a push to the screen. On a LAN, Server adds a few milliseconds.

**Trade-offs we accept:**
- If the screen's connection drops, the UI freezes until it reconnects. The game keeps running, because the state lives in `GameRegistry` and not in the connection.
- The server does the rendering. With one game screen and a few viewers this is negligible.
- Each UI update is a small message from the server. Update only what changed (for example one tile) instead of re-rendering the whole 32 × 32 board.

## Why the API and Blazor are one project

`GameRegistry` is a singleton that holds all live state: lobbies, players, the board and scores. The Oplà changes that state through the HTTP endpoints, and the UI has to read and react to the same state.

A singleton exists **once per process**. Two projects would be two processes with two separate registries, and the UI would never see what the controllers did. The alternatives are worse:
- The UI calls the API over HTTP: this brings back DTOs for everything, an extra network hop on every update, CORS, and polling to learn about changes.
- Shared state in a database or Redis: this adds latency and complexity to a game that has to feel fast.

One host solves it: the Oplà calls `/api/...`, the UI injects `GameRegistry`, and both use the same object. A change from a controller can notify the screen immediately.

### Why this still separates concerns

One *process* is not one *layer*. The boundaries are enforced by project references and folders, not by running separate servers:

- **`Domain` knows nothing about the web.** No ASP.NET, no Blazor, no HTTP, no database. It is fully testable without a server.
- **`Shared` is the contract with the Oplà firmware.** It holds only plain records, and the field names are fixed by the firmware.
- **`Endpoints/` is a thin adapter.** It validates input, calls Domain, and maps the result to a Shared DTO. It contains no game rules.
- **`Pages/` is presentation only.** Components show state and send user intent to Domain. They contain no game rules.
- **The dependency direction is one-way:** the host depends on Domain and Shared, never the reverse.

If the game ever needs several server instances, this is the point to revisit: live state would then have to move out of memory.

## Rules

- **Domain stays clean.** No `Microsoft.AspNetCore.*`, no `HttpClient`, no UI types.
- **Game rules live in Domain only.** Not in endpoints, not in Razor components.
- **Endpoints stay thin.** Parse → call Domain → map to DTO → return.
- **Never render hidden information.** Blazor Server keeps code and data on the server, but whatever is rendered into the page is sent to the browser. Do not put the contents of undug tiles (treasure or mine) into the markup, not even hidden with CSS. Render only what the player is allowed to see.
- **Do not change Oplà field names** without changing the firmware. The tests check this.
- **No secrets in `wwwroot`.** Its files are downloaded by the browser.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Trust the HTTPS dev certificate (once per machine):
  ```bash
  dotnet dev-certs https --trust
  ```

## Getting Started

```bash
git clone <your-repo-url>
cd TreasureSweeper
dotnet restore
dotnet build
```

## Running the App

### Visual Studio
Set **Blazor** as the startup project and press **F5**.

### Command line / VS Code / Rider
```powershell
.\run.ps1
```
or
```bash
dotnet run --project src/Blazor --launch-profile https
```

| What        | URL                                      |
| ----------- | ---------------------------------------- |
| Game screen | https://localhost:7033                   |
| Oplà API    | http://localhost:5288/api/...            |
| Swagger UI  | https://localhost:7033/swagger (Development only) |
| OpenAPI     | https://localhost:7033/openapi/v1.json   |

**The Oplà uses plain HTTP.** HTTPS redirection is deliberately off, because the controller cannot follow a redirect and HTTPS on the board needs root certificates. Use the `http://` address on port **5288**. On a real LAN, set the Oplà's host to your PC's IP address (not `localhost`) and allow port 5288 through the firewall.

## Tests

```
Tests/
├── Domain/   # Unit tests for game rules — fast, no web server
└── Api/      # Integration tests — runs the app in memory (WebApplicationFactory<Program>)
```

### Running the tests
```bash
dotnet test                                                     # all tests
dotnet test Tests --filter "FullyQualifiedName~Tests.Domain"    # Domain tests only
dotnet test Tests --filter "FullyQualifiedName~GameTests.Join"  # one group of tests
```
In Visual Studio, use **Test → Test Explorer**.

### Conventions
- One test class per type under test, e.g. `GameTests` for `Game`.
- Test names follow `Method_Situation_ExpectedResult`, e.g. `Join_NinthPlayer_FailsWithGameFull`.
- Put game rules in **Domain** tests. Use **Api** tests only for HTTP details: status codes, JSON field names, headers.
- API tests share one `GameRegistry` per test class. Each test must create its own game (random PIN) and never hard-code a PIN such as `1234`.
- Check controller responses as raw `JsonElement`, not deserialized DTOs. The Oplà firmware only reads fixed field names, so a renamed or extra field must fail the test.

### Manual testing
`src/Blazor/API.http` calls every endpoint, and each request is labelled with its expected result.
Start the app with `dotnet run --project src/Blazor --launch-profile http`, then send the requests
**top to bottom** from VS Code (REST Client extension), Visual Studio or Rider, since later requests depend on earlier ones.

## Configuration

| Setting | File | Notes |
| ------- | ---- | ----- |
| Ports (`applicationUrl`) | `src/Blazor/Properties/launchSettings.json` | The Oplà firmware and `API.http` must use the same HTTP port (5288) |
| Logging, other app settings | `src/Blazor/appsettings*.json` | |

There is no CORS and no `ApiBaseUrl` setting anymore: the UI and the API are the same app.

## Adding a Feature (workflow)

1. **Shared** – add a DTO only if the Oplà reads or sends it, e.g. `RevealTileRequest`. The UI can use Domain objects or small view models.
2. **Domain** – implement the rule and its unit tests (no web or UI code).
3. **Endpoints** – if the Oplà needs it, add an endpoint that maps Domain → DTO.
4. **Pages** – inject `GameRegistry` in the component and render only what the player may see.
5. **Tests** – Domain tests for the rule, and an API test if the Oplà reads the response.

Agree on the DTO and the Domain API first (steps 1–2); then the screen, the endpoints and the firmware can be built in parallel.

## Troubleshooting

| Symptom | Fix |
| ------- | --- |
| Oplà gets a redirect or can't connect | Use `http://<PC-IP>:5288`, not https; check the firewall |
| *NET::ERR_CERT_AUTHORITY_INVALID* in the browser | `dotnet dev-certs https --trust`, restart browser |
| *Address already in use* | A previous instance is still running — stop the `Blazor.exe` process |
| Build error *file is being used by another process* | Two builds ran at once — use `run.ps1` (it builds once) |
| Screen stops updating | The browser lost its connection; reload. Game state is kept on the server |

## Building for Production

```bash
dotnet publish src/Blazor -c Release -o ./publish
```
There is one app to deploy. Live game state is in memory, so run a **single instance**; a restart ends all running games.

