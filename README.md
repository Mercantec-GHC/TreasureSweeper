# Treasure Sweeper

A full-stack web application built with a Blazor WebAssembly front end and an ASP.NET Core Web API back end, targeting .NET 10.

## Tech Stack

| Layer    | Project       | Technology / purpose                                              |
| -------- | ------------- | ----------------------------------------------------------------- |
| Frontend | `src/Blazor`  | Blazor WebAssembly UI                                             |
| Backend  | `src/API`     | ASP.NET Core Web API (minimal APIs + OpenAPI)                     |
| Contracts| `src/Shared`  | DTOs / request & response records shared by API and Blazor        |
| Domain   | `src/Domain`  | Game rules: board, tiles, treasure/mine placement (server only)   |
| Platform | —             | .NET 10 (`net10.0`), C#                                           |
| Solution | `Treasure-sweeper.slnx` | Visual Studio solution (XML format)                     |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Trust the HTTPS dev certificate (once per machine):
  ```bash
  dotnet dev-certs https --trust
  ```

## Project Structure

```
TreasureSweeper/
├── Treasure-sweeper.slnx     # Solution
├── Treasure-sweeper.slnLaunch# Shared VS startup profile (API + Blazor)
├── run.ps1                   # Starts API + Blazor from the CLI
└── src/
    ├── API/      # Web API — references Shared + Domain
    ├── Blazor/   # Frontend — references Shared ONLY
    ├── Domain/   # Game logic — no project references
    └── Shared/   # DTOs — no project references, no ASP.NET dependencies
```

```
Blazor ──► Shared ◄── API ──► Domain
```

**Rules**
- Blazor must **not** reference Domain. The server owns the board; the client only
  receives what the player is allowed to see (via Shared DTOs). Otherwise the
  treasure/mine positions would be readable in the browser.
- Shared contains only plain records/classes so it runs in WebAssembly.

## Getting Started

1. **Clone the repository**

   ```bash
   git clone <your-repo-url>
   cd TreasureSweeper
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Build the solution**

   ```bash
   dotnet build
   ```

## Running the App

### Visual Studio
Select the **API + Blazor** startup profile
(right-click solution → *Configure Startup Projects…*) and press **F5**.

### Command line / VS Code / Rider
```powershell
.\run.ps1
```
If script execution is blocked: `powershell -ExecutionPolicy Bypass -File .\run.ps1`.
Press **Ctrl+C** to stop both.

### Manually (two terminals)
```bash
dotnet run --project src/API --launch-profile https
dotnet run --project src/Blazor --launch-profile https
```

| App    | URL                       |
| ------ | ------------------------- |
| Blazor | https://localhost:7033    |
| API    | https://localhost:7235    |
| OpenAPI| https://localhost:7235/openapi/v1.json |

## Configuration

| Setting | File | Must match |
| ------- | ---- | ---------- |
| `ApiBaseUrl` | `src/Blazor/wwwroot/appsettings.Development.json` | API URL |
| `Cors:AllowedOrigins` | `src/API/appsettings.Development.json` | Blazor URL(s) |

If you change a port in a `launchSettings.json`, update the matching setting above.
⚠️ Files in `Blazor/wwwroot` are downloaded by the browser — never put secrets there.

## Adding a Feature (workflow)

1. **Shared** – add the DTO, e.g. `RevealTileRequest`, `BoardDto`.
2. **Domain** – implement the rule (no web/HTTP code here).
3. **API** – add the endpoint: map Domain → DTO.
4. **Blazor** – call the endpoint with `HttpClient` + `GetFromJsonAsync<BoardDto>()`.

Agree on the DTO first (step 1) — then frontend and backend can work in parallel.

## Troubleshooting

| Symptom | Fix |
| ------- | --- |
| Browser console: *blocked by CORS policy* | Blazor URL missing from `Cors:AllowedOrigins`, or http/https profile mismatch |
| *NET::ERR_CERT_AUTHORITY_INVALID* / fetch fails | `dotnet dev-certs https --trust`, restart browser |
| *Address already in use* | A previous API is still running — stop the `API.exe` process |
| Build error *file is being used by another process* | Two builds ran at once — use `run.ps1` (it builds once) |

## Building for Production

```bash
dotnet publish src/API -c Release -o ./publish/api
dotnet publish src/Blazor -c Release -o ./publish/blazor   # static files in publish/blazor/wwwroot
```
Set the production `ApiBaseUrl` in `src/Blazor/wwwroot/appsettings.json` and the
production Blazor origin in the API's `Cors:AllowedOrigins`.

## License

Add your license information here.
