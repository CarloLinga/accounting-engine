using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Infrastructure.Persistence;

public class AccountingDbContext : DbContext, IAccountingDbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options) 
        : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<SalesJournal> SalesJournals => Set<SalesJournal>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<SourceRule> SourceRules => Set<SourceRule>();
    public DbSet<SourceRuleLine> SourceRuleLines => Set<SourceRuleLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically discovers and applies all IEntityTypeConfiguration implementations in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
    }
}
