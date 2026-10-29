# Treasure Sweeper

A full-stack web application built with a Blazor WebAssembly front end and an ASP.NET Core Web API back end, targeting .NET 10.

## Tech Stack

| Layer    | Project        | Technology                                      |
| -------- | -------------- | ----------------------------------------------- |
| Frontend | `src/Blazor`   | Blazor WebAssembly (`Microsoft.AspNetCore.Components.WebAssembly`) |
| Backend  | `src/API`      | ASP.NET Core Web API with OpenAPI (`Microsoft.AspNetCore.OpenApi`) |
| Platform | —              | .NET 10 (`net10.0`), C#                         |
| Solution | `Treasure-sweeper.slnx` | Visual Studio solution (XML format)    |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A supported IDE (Visual Studio 2022/2026, Visual Studio Code, or JetBrains Rider) — optional

## Project Structure

```
TreasureSweeper/
├── Treasure-sweeper.slnx   # Solution file
└── src/
	├── Blazor/             # Blazor WebAssembly frontend (Blazor.csproj)
	└── API/               # ASP.NET Core Web API backend (API.csproj)
```

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

Run the backend API:

```bash
dotnet run --project src/API/API.csproj
```

In a separate terminal, run the Blazor WebAssembly frontend:

```bash
dotnet run --project src/Blazor/Blazor.csproj
```

The console output will show the local URLs (for example, `https://localhost:xxxx`). When running the API, the OpenAPI document is available at `/openapi/v1.json` (in the Development environment).

## Building for Production

```bash
dotnet publish src/API/API.csproj -c Release -o ./publish
```

## License

Add your license information here.
