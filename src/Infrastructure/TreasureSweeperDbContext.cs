using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

/// <summary>
/// EF Core context for Treasure Sweeper. Intentionally empty for now:
/// entities, the schema and migrations come in a follow-up issue.
/// </summary>
public sealed class TreasureSweeperDbContext(DbContextOptions<TreasureSweeperDbContext> options)
    : DbContext(options);
