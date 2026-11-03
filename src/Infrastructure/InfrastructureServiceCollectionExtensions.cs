using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Registers the EF Core DbContext (PostgreSQL) and a "database" health check.
    /// The connection string (ConnectionStrings:Default) is read lazily, the first time the
    /// database is needed, so the app can start, and tests can run, without a database.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<TreasureSweeperDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured. " +
                    "See the 'Database' section in README.md.");

            options.UseNpgsql(connectionString);
        });

		services.AddHealthChecks().AddDbContextCheck<TreasureSweeperDbContext>(
		"database",
		customTestQuery: async (db, ct) =>
		{
			await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
			return true;
		});

		return services;
    }
}
