using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Interfaces;

public interface IAccountingDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<SourceRule> SourceRules { get; }
    DbSet<SourceRuleLine> SourceRuleLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
