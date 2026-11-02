using API.Endpoints;
using Domain.Games;

var builder = WebApplication.CreateBuilder(args);
const string BlazorCors = "BlazorClient";
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddCors(o => o.AddPolicy(BlazorCors, p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<GameRegistry>(); // DEPENDENCY INJECTING GameRegistry


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(BlazorCors);
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "TreasureSweeper API"));
}

app.UseHttpsRedirection();


app.MapGameEndpoints();
app.MapControllerEndpoints();

app.Run();
