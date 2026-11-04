using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

/// <summary>
/// EF Core context for Treasure Sweeper. Stores players, finished rounds and the
/// per-player result of each round. Live game state stays in memory, not here.
/// </summary>
public sealed class TreasureSweeperDbContext(DbContextOptions<TreasureSweeperDbContext> options)
    : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<RoundEntity> Rounds => Set<RoundEntity>();
    public DbSet<RoundPlayerEntity> RoundPlayers => Set<RoundPlayerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TreasureSweeperDbContext).Assembly);
    }
}
