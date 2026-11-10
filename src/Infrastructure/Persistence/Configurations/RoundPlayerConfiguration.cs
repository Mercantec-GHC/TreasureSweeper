using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class RoundPlayerConfiguration : IEntityTypeConfiguration<RoundPlayerEntity>
{
	public void Configure(EntityTypeBuilder<RoundPlayerEntity> b)
	{
		// Composite key: one result row per player per round
		b.HasKey(rp => new { rp.RoundId, rp.PlayerId });

		// Deleting a round deletes its result rows
		b.HasOne(rp => rp.Round)
			.WithMany(r => r.PlayerResults)
			.HasForeignKey(rp => rp.RoundId)
			.OnDelete(DeleteBehavior.Cascade);

		// A player with results can't be deleted
		b.HasOne(rp => rp.Player)
			.WithMany(p => p.RoundResults)
			.HasForeignKey(rp => rp.PlayerId)
			.OnDelete(DeleteBehavior.Restrict);

		// Leaderboard lookups
		b.HasIndex(rp => rp.PlayerId);
		b.HasIndex(rp => rp.Score);

		// Sanity rules enforced by the database itself
		b.ToTable("RoundPlayers", t =>
		{
			t.HasCheckConstraint("CK_RoundPlayers_Placement", "\"Placement\" >= 1");
			t.HasCheckConstraint("CK_RoundPlayers_TreasuresFound", "\"TreasuresFound\" >= 0");
			t.HasCheckConstraint("CK_RoundPlayers_FreezeMinesHit", "\"FreezeMinesHit\" >= 0");
			t.HasCheckConstraint("CK_RoundPlayers_PointMinesHit", "\"PointMinesHit\" >= 0");
			t.HasCheckConstraint("CK_RoundPlayers_MinesAvoided", "\"MinesAvoided\" >= 0");
			t.HasCheckConstraint("CK_RoundPlayers_TotalSteps", "\"TotalSteps\" >= 0");
			t.HasCheckConstraint("CK_RoundPlayers_FewestStepsToTreasure", "\"FewestStepsToTreasure\" >= 0");
		});
	}
}