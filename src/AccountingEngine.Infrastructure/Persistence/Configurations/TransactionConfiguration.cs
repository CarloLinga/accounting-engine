using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasIndex(t => t.PostedAt);
        builder.HasIndex(t => t.Reference);

        builder.Property(t => t.SourceType)
               .HasMaxLength(50)
               .IsRequired();
    }
}
