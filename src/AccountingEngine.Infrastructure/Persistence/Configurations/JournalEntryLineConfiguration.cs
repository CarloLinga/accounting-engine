using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("journal_entry_lines", t =>
        {
            t.HasCheckConstraint(
                "chk_debit_xor_credit",
                "(debit > 0 AND credit = 0) OR (credit > 0 AND debit = 0)"
            );
        });

        builder.Property(j => j.Description)
               .HasMaxLength(500);

        builder.Property(j => j.Debit)
               .HasPrecision(15, 2);

        builder.Property(j => j.Credit)
               .HasPrecision(15, 2);

        builder.HasIndex(j => new { j.TransactionId, j.Sequence })
               .IsUnique();

        builder.HasOne(j => j.Transaction)
               .WithMany(t => t.JournalEntryLines)
               .HasForeignKey(j => j.TransactionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.Account)
               .WithMany(a => a.JournalEntryLines)
               .HasForeignKey(j => j.AccountId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}