using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class RoundConfiguration : IEntityTypeConfiguration<RoundEntity>
{
	public void Configure(EntityTypeBuilder<RoundEntity> b)
	{
		b.HasKey(p => p.Id);
		b.HasIndex(p => p.EndedAt);
		b.Property(p => p.EndReason).HasConversion<string>().HasMaxLength(32);
	}
}