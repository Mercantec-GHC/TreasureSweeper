using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<PlayerEntity>
{
	public void Configure(EntityTypeBuilder<PlayerEntity> b)
	{
		b.HasKey(p => p.Id);
		b.Property(p => p.Name).IsRequired().HasMaxLength(40);
		b.Property(p => p.DeviceId).HasMaxLength(64);
		b.HasIndex(p => p.Name).IsUnique();
		b.HasIndex(p => p.DeviceId).IsUnique();
	}
}