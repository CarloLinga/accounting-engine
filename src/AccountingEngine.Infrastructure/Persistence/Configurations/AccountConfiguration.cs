using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingEngine.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasIndex(a => a.Code)
               .IsUnique();

        builder.Property(a => a.Type)
               .HasConversion<string>(); // Store enum as string

        builder.Property(a => a.Statement)
               .HasConversion<string>();

        builder.Property(a => a.BalanceSheetClass)
               .HasConversion<string>();

        builder.Property(a => a.IncomeStatementClass)
               .HasConversion<string>();

        builder.Property(a => a.CashFlowActivity)
               .HasConversion<string>();

        // Header -> detail hierarchy for report grouping.
        builder.HasOne(a => a.ParentAccount)
               .WithMany(a => a.ChildAccounts)
               .HasForeignKey(a => a.ParentAccountId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.ParentAccountId, a.DisplayOrder });
    }
}
