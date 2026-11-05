using API.Endpoints;
using Blazor;
using Domain.Games;
using Infrastructure;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<GameRegistry>();
builder.Services.AddInfrastructure();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The container starts against an empty Postgres volume. Apply migrations once
    // the process is up so /health and persisted rounds work without a manual step.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<TreasureSweeperDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
	app.MapOpenApi();
	app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "TreasureSweeper API"));

	// #38 — dev-only sample data so the UI and API have something to show.
	await using var scope = app.Services.CreateAsyncScope();
	var store = scope.ServiceProvider.GetRequiredService<IRoundResultStore>();
	var db = scope.ServiceProvider.GetRequiredService<TreasureSweeperDbContext>();
	try { await DatabaseSeeder.SeedAsync(store, db); }
	catch (Exception ex) { app.Logger.LogWarning(ex, "Skipped dev seeding (is the database migrated and running?)"); }
}

// No UseHttpsRedirection: the Opla controllers call plain http on the LAN and cannot follow a redirect.

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapGameEndpoints();
app.MapControllerEndpoints();
app.MapLeaderboardEndpoints();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
