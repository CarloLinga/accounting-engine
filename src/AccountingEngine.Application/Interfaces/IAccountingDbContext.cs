using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Interfaces;

public interface IAccountingDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<SalesJournal> SalesJournals { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<SourceRule> SourceRules { get; }
    DbSet<SourceRuleLine> SourceRuleLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
