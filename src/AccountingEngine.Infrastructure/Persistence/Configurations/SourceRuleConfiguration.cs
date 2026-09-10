using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class SourceRuleConfiguration : IEntityTypeConfiguration<SourceRule>
{
    public void Configure(EntityTypeBuilder<SourceRule> builder)
    {
        builder.ToTable("source_rules");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.SourceType)
               .IsUnique();

        builder.Property(e => e.SourceType)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(e => e.IsManualEntryAllowed)
               .HasDefaultValue(false);

       builder.HasMany(e => e.RuleLines)
               .WithOne(l => l.SourceRule)
               .HasForeignKey(l => l.SourceRuleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
