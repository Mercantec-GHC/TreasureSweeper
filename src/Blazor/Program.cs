using API.Endpoints;
using Blazor;
using Domain.Games;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

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
}

// No UseHttpsRedirection: the Opla controllers call plain http on the LAN and cannot follow a redirect.

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapGameEndpoints();
app.MapControllerEndpoints();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
