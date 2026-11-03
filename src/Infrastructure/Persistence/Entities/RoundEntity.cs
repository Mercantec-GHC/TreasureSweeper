using Shared.Games;

namespace Infrastructure.Persistence.Entities;

public class RoundEntity
{
	public Guid Id { get; set; }
	public DateTimeOffset StartedAt { get; set; }
	public DateTimeOffset EndedAt { get; set; }
	public int PlayerCount { get; set; }
	public RoundEndReason EndReason { get; set; }
	public ICollection<RoundPlayerEntity> PlayerResults { get; set; } = new List<RoundPlayerEntity>();
}
