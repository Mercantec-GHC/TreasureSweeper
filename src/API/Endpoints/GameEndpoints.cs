using API.Mapping;
using Domain.Games;
using Microsoft.AspNetCore.Http.HttpResults;
using Shared.Games;

namespace API.Endpoints;

internal static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games").WithTags("Games");

        group.MapPost("/", CreateGame)
            .WithName("CreateGame")
            .WithSummary("Create a game")
            .WithDescription("Allocates a random 4-digit PIN, or uses the requested one.");

        group.MapGet("/{pin}", GetGame)
            .WithName("GetGame")
            .WithSummary("Get a game's lobby state");

        group.MapPost("/{pin}/start", (string pin, GameRegistry registry) => Apply(pin, registry, g => g.Start()))
            .WithName("StartRound")
            .WithSummary("Start the round (needs at least one player)");

        group.MapPost("/{pin}/end", (string pin, GameRegistry registry) => Apply(pin, registry, g => g.End()))
            .WithName("EndRound")
            .WithSummary("End the running round");

        group.MapPost("/{pin}/reset", (string pin, GameRegistry registry) => Apply(pin, registry, g => g.Reset()))
            .WithName("ResetRound")
            .WithSummary("Return to the lobby for a new round, keeping players");

        return app;
    }

    private static Results<Created<GameResponse>, ProblemHttpResult> CreateGame(
        CreateGameRequest? request, GameRegistry registry
    )
    {
        var result = registry.Create(request?.Pin);
        if (!result.Success)
        {
            var (status, message) = result.Error!.Value.ToHttp();
            return TypedResults.Problem(message, statusCode: status);
        }

        var game = result.Game!;
        return TypedResults.Created($"/api/games/{game.Pin}", game.ToResponse());
    }

    private static Results<Ok<GameResponse>, NotFound> GetGame(string pin, GameRegistry registry) =>
        registry.Find(pin) is { } game
            ? TypedResults.Ok(game.ToResponse())
            : TypedResults.NotFound();

    private static Results<Ok<GameResponse>, NotFound, ProblemHttpResult> Apply(
        string pin, GameRegistry registry, Func<Game, GameResult> action)
    {
        var game = registry.Find(pin);
        if (game is null)
            return TypedResults.NotFound();

        var result = action(game);
        if (!result.Success)
        {
            var (status, message) = result.Error!.Value.ToHttp();
            return TypedResults.Problem(message, statusCode: status);
        }

        return TypedResults.Ok(game.ToResponse());
    }
}