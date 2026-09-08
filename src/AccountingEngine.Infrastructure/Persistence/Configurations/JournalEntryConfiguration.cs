using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Reference)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(j => j.Reference)
            .IsUnique();

        builder.Property(j => j.SourceType)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(t => t.PostedAt);

        builder.HasMany(j => j.JournalEntryLines)
            .WithOne()
            .HasForeignKey(l => l.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
