namespace Infrastructure.Persistence.Entities;

public class RoundPlayerEntity
{
	public Guid RoundId { get; set; }
	public Guid PlayerId { get; set; }
	public int Score { get; set; }
	public int Placement { get; set; }
	public int TreasuresFound { get; set; }
	public int FreezeMinesHit { get; set; }
	public int PointMinesHit { get; set; }
	public int MinesAvoided { get; set; }
	public int TotalSteps { get; set; }
	public int? FewestStepsToTreasure { get; set; }
	public PlayerEntity Player { get; set; } = null!;
	public RoundEntity Round { get; set; } = null!;
}