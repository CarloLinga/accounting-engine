using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

/// <summary>
/// Shared per-account (debit, credit) aggregation over an arbitrary posting
/// window. Extracted so Trial Balance, Ledger, Balance Sheet, Income Statement
/// and Cash Flow all compute from one consistent source.
/// </summary>
public class AccountBalanceProvider : IAccountBalanceProvider
{
    private readonly IAccountingDbContext _dbContext;

    public AccountBalanceProvider(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> GetTotalsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        // Snapshot of journal entry dates so the window is applied in memory:
        // the SQLite provider used by the service-level tests cannot translate
        // DateTimeOffset comparisons to SQL. The per-account GROUP BY + SUM
        // still runs on the database, so Postgres queries stay efficient.
        var index = await _dbContext.JournalEntries.AsNoTracking()
            .Select(j => new { j.Id, j.PostedAt })
            .ToListAsync(cancellationToken);

        var entryIds = index
            .Where(e => (!from.HasValue || e.PostedAt >= from.Value)
                     && (!to.HasValue || e.PostedAt <= to.Value))
            .Select(e => e.Id)
            .ToList();

        if (entryIds.Count == 0)
            return new Dictionary<Guid, (decimal Debit, decimal Credit)>();

        var rows = await _dbContext.JournalEntryLines.AsNoTracking()
            .Where(l => entryIds.Contains(l.JournalEntryId))
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(l => l.Debit),
                Credit = g.Sum(l => l.Credit)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.AccountId, r => (r.Debit, r.Credit));
    }

    /// <summary>
    /// Returns the balance on the account type's natural side
    /// (Asset/Expense → Debit − Credit; Liability/Equity/Revenue → Credit − Debit).
    /// </summary>
    public static decimal Normalize(AccountType accountType, decimal debit, decimal credit) =>
        accountType.GetNormalBalance() == PostingType.Debit ? debit - credit : credit - debit;
}
