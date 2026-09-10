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
            // CAST(... AS NUMERIC) keeps this constraint portable: numeric(15,2) is a
            // real numeric on Postgres but is stored as TEXT-affinity by the SQLite
            // provider, where `credit = 0` would otherwise be a text comparison.
            t.HasCheckConstraint(
                "chk_debit_xor_credit",
                "(CAST(debit AS NUMERIC) > 0 AND CAST(credit AS NUMERIC) = 0)"
                + " OR (CAST(credit AS NUMERIC) > 0 AND CAST(debit AS NUMERIC) = 0)"
            );
        });

        builder.Property(j => j.Description)
               .HasMaxLength(500);

        builder.Property(j => j.Debit)
               .HasPrecision(15, 2);

        builder.Property(j => j.Credit)
               .HasPrecision(15, 2);

        builder.HasIndex(j => new { j.Id, j.Sequence })
               .IsUnique();

        builder.HasOne(j => j.JournalEntry)
               .WithMany(t => t.JournalEntryLines)
               .HasForeignKey(j => j.JournalEntryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.Account)
               .WithMany(a => a.JournalEntryLines)
               .HasForeignKey(j => j.AccountId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}