using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class SourceRuleLineConfiguration : IEntityTypeConfiguration<SourceRuleLine>
{
    public void Configure(EntityTypeBuilder<SourceRuleLine> builder)
    {
        builder.ToTable("source_rule_lines");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryType)
               .HasConversion<string>() // Stores "Debit" or "Credit"
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(e => e.AmountType)
               .HasMaxLength(50)
               .IsRequired();

        builder.HasOne(e => e.Account)
               .WithMany()
               .HasForeignKey(e => e.AccountId)
               .OnDelete(DeleteBehavior.Restrict); // Prevents deleting accounts configured in rules
    }
}
