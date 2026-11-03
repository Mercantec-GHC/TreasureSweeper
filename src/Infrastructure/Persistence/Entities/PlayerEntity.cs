namespace Infrastructure.Persistence.Entities;

public class PlayerEntity
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? DeviceId { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public ICollection<RoundPlayerEntity> RoundResults { get; set; } = new List<RoundPlayerEntity>();
}