# Treasure Sweeper

A multiplayer "treasure hunt meets Minesweeper" party game for up to 8 players. Each player steers with an Arduino Oplà controller, and the shared game screen shows the board, scores and leaderboard.

It is a single ASP.NET Core application on **.NET 10** that serves a **Blazor Server** UI and the HTTP endpoints the Oplà controllers talk to. Both run in the same process and share the same live game state.

## ▶ Quick start: get a game running

The short version for a game night. Details are in the sections further down.

### 1. Start the database, API and Blazor

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Docker Desktop.
2. Once per clone: `Copy-Item .env.example .env`, set `POSTGRES_PASSWORD`, and give the app its connection string (see [Database](#database)).
3. Start PostgreSQL:
   ```powershell
   docker compose -f docker-compose.yml -f docker-compose.local.yml up -d database
   ```
4. Apply migrations (first time and after pulling new ones):
   ```powershell
   dotnet ef database update --project src/Infrastructure --startup-project src/Blazor
   ```
5. Start the app. The API and the Blazor UI are the same process:
   ```powershell
   .\run.ps1
   ```
   Game screen: https://localhost:7033. Controller API: `http://<PC-IP>:5288/api/controller/...`

The game itself runs without a database. Only the leaderboard and history need it.

### 2. Flash and configure an Oplà controller

1. Install the [Arduino IDE](https://www.arduino.cc/en/software), the board package **Arduino SAMD Boards** (the Oplà is an MKR WiFi 1010), and the libraries `Arduino_MKRIoTCarrier`, `WiFiNINA`, `ArduinoJson` and `ArduinoHttpClient`.
2. Open `Arduino/Arduino.ino` and edit `Arduino/config.h`:

   | Setting | Meaning |
   | ------- | ------- |
   | `WIFI_SSID`, `WIFI_PASS` | The Wi-Fi network the controller joins |
   | `PLAYER_NAME` | Name shown on the screen (empty gives `Arduino <number>`) |
   | `GAME_PIN` | The PIN of the game this controller joins |
   | `SERVER_HOST`, `SERVER_PORT` | The backend. Deployed: `treasuresweeper.mercantec.tech` on `443` |
   | `GAME_PATH` | Path prefix in front of `/api/...`. Empty for this game |
   | `USE_CASE` | `true` if the Oplà is in its plastic case (adjusts touch sensitivity) |
   | `HEARTBEAT_MS` | How often the controller says "still here" (2000 ms) |

3. Select board **Arduino MKR WiFi 1010** and its port, then press **Upload**.
4. On power-up the controller connects to Wi-Fi, joins the game with `GAME_PIN` and keeps the session alive. If it can't join yet (for example the game doesn't exist), it keeps trying.

The firmware talks HTTPS (`WiFiSSLClient`). The protocol is described in [docs/controller-api.md](docs/controller-api.md).

**No hardware?** [tools/ControllerSimulator](tools/ControllerSimulator/README.md) pretends to be 1–8 controllers from the keyboard.

### 3. Create a game, join controllers and start a round

1. **Create the game with the controllers' PIN.** Every controller has a fixed `GAME_PIN`, so the game must use that PIN:
   ```http
   POST /api/games?pin=1515
   ```
   Use Swagger (`/swagger`) or `src/Blazor/API.http`. (**Host Game** on the Game Screen creates a game with a random PIN. That only works for controllers flashed with that PIN, or for the simulator with `--pin`.)
2. **Open the Game Screen** (`/game-screen`), type the PIN and press **Join Game**. The lobby lists the players.
3. **Turn on the controllers.** Each one joins by itself and appears in the lobby. Up to 8 players; joining closes when the round starts.
4. **Start the round** with `POST /api/games/{pin}/start` (or **Start Game** on the screen if you hosted it there). The round lasts 8 minutes or until the last treasure is dug up.
5. **Next round:** `POST /api/games/{pin}/reset` returns to the lobby and keeps the players.

### 4. Controller button mapping

| Touch pad | Action | Notes |
| --------- | ------ | ----- |
| **0** | Scan | Scans the tile in front: shows its content or the number of mines around it |
| **1** | Turn left | 90° |
| **2** | Move | One step forward in the facing direction |
| **3** | Turn right | 90° |
| **4** | Dig | Digs the tile in front |

| Light | Meaning |
| ----- | ------- |
| Red | Scan: a mine is on the tile in front |
| Yellow | Dig: treasure found |
| Red | Point mine set off (−25) |
| Blue | Freeze mine set off: frozen for 5 s |

The full rules are in [docs/game-rules.md](docs/game-rules.md). The home page of the app shows a short player-friendly version.

## Tech Stack

| Layer      | Project        | Technology / purpose                                                          |
| ---------- | -------------- | ----------------------------------------------------------------------------- |
| Web host   | `src/Blazor`   | Blazor Server UI **and** minimal-API endpoints for the Oplà (one process)     |
| Domain     | `src/Domain`   | Game rules: lobby, players, board, treasure/mine placement. No web code       |
EF Core + Npgsql (PostgreSQL): `DbContext`, migrations, round results, leaderboard queries and health check |
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
Blazor ──► Domain ──► Shared
   ├─────► Shared
   └─────► Infrastructure ──► Domain, Shared
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
    ├── Infrastructure/       # Database access (EF Core + PostgreSQL) — no web or UI code
    ├── Domain/               # Game logic — references only Shared
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
- **Domain never references Infrastructure.** The database depends on the rules, not the other way round.
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

## Database

PostgreSQL runs in Docker. The app talks to it through EF Core (`src/Infrastructure`).

**One-time setup (per developer)**

1. Copy `.env.example` to `.env` (`Copy-Item .env.example .env`) and set your own `POSTGRES_PASSWORD`. `.env` is git-ignored, so every clone needs its own; never commit it.
2. Start the database: `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d database` (the base file alone joins the Dokploy server network, which doesn't exist locally)
3. Check it: `docker compose ps` should show `healthy`.
4. Give the app its connection string, using the same user, password, database and port as your `.env`:
   ```powershell
   dotnet user-secrets init --project src/Blazor
   dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5432;Database=treasuresweeper;Username=treasure;Password=<your password>" --project src/Blazor
   ```
   Alternatively set the environment variable `ConnectionStrings__Default`.
   Use `127.0.0.1`, not `localhost`: the container port is published on IPv4 only, and `localhost` can resolve to IPv6 (`[::1]`), which fails with *Failed to connect to [::1]:5432*.
5. Run the app and open `https://localhost:7033/health`. `Healthy` means the app can reach the database.

### Migrations

The schema is managed with EF Core migrations (`src/Infrastructure/Persistence/Migrations`). The tables are `Players`, `Rounds` and `RoundPlayers`. They hold only players and finished rounds; live game state stays in memory.

Install the tool once: `dotnet tool install --global dotnet-ef`

| Task | Command |
| ---- | ------- |
| Apply migrations to your database | `dotnet ef database update --project src/Infrastructure --startup-project src/Blazor` |
| Create a migration after changing entities or configurations | `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Blazor --output-dir Persistence/Migrations` |
| Undo the latest migration (only if not pushed) | `dotnet ef migrations remove --project src/Infrastructure --startup-project src/Blazor` |
| Check that the model matches the migrations | `dotnet ef migrations has-pending-model-changes --project src/Infrastructure --startup-project src/Blazor` |
| Reset your local database | `docker compose -f docker-compose.yml -f docker-compose.local.yml down -v`, start it again, then `database update` |

Rules:
- Run `database update` after pulling new migrations.
- Never edit a migration that has been pushed. Add a new one instead.
- One migration per change. Commit all three files: the migration, its `.Designer.cs` and `TreasureSweeperDbContextModelSnapshot.cs`.
- On a first run, a logged `fail` for `SELECT ... FROM "__EFMigrationsHistory"` is normal: EF is checking whether the history table exists.

**Useful commands**

| Command | What it does |
| ------- | ------------ |
| `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d database` / `... down` | Start / stop the database. The data is kept in the `pgdata` volume |
| `docker compose down -v` | Stop **and delete** all data (also needed after changing `POSTGRES_*` in `.env`) |
| `docker compose logs database` | Show the database log |

**Notes**
- The connection string is read the first time the database is needed, so the app starts, and the tests run, without a database. `/health` returns `503 Unhealthy` until it is configured and reachable.
- Only the `Default` connection string is used. Never commit passwords or real connection strings.
- In Development the app seeds a few example rounds into an empty database, so the leaderboard has something to show.

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
| `/health` returns 503, log says *Failed to connect to [::1]:5432* | Use `Host=127.0.0.1` in the connection string |
| `/health` returns 503 | No `.env` on this clone? Is `docker compose ps` healthy? Is `ConnectionStrings:Default` set (user secrets) and does it match `.env`? |
| Screen stops updating | The browser lost its connection; reload. Game state is kept on the server |

## Building for Production

```bash
dotnet publish src/Blazor -c Release -o ./publish
```
There is one app to deploy. Live game state is in memory, so run a **single instance**; a restart ends all running games.

