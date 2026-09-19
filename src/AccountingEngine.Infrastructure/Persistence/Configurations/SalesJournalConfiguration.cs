using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class SalesJournalConfiguration : IEntityTypeConfiguration<SalesJournal>
{
    public void Configure(EntityTypeBuilder<SalesJournal> builder)
    {
        builder.ToTable("sales_journals");

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.InvoiceNo)
            .IsUnique();

        builder.Property(e => e.InvoiceNo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Customer)
            .HasMaxLength(255);

        builder.Property(e => e.TaxIDNo)
            .HasMaxLength(50);

        builder.Property(e => e.InvoiceAmount)
            .HasPrecision(15, 2);

        builder.Property(e => e.VatAmount)
            .HasPrecision(15, 2);

        builder.Property(e => e.VATableSale)
            .HasColumnName("vatable_sale")
            .HasPrecision(15, 2);

        builder.Property(e => e.ZeroRatedSale)
            .HasPrecision(15, 2);

        builder.Property(e => e.VatExemptSale)
            .HasPrecision(15, 2);
    }
}
