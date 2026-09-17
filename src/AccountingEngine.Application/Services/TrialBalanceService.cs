using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

public class TrialBalanceService : ITrialBalanceService
{
    private readonly IAccountingDbContext _dbContext;

    public TrialBalanceService(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<TrialBalanceResponse>> GenerateTrialBalanceAsync(
        DateTimeOffset? asOf = null,
        int? fiscalYear = null,
        bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = asOf ?? DateTimeOffset.UtcNow;
        var year = fiscalYear ?? asOfDate.Year;
        var periodStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        if (asOfDate < periodStart)
        {
            return ServiceResult<TrialBalanceResponse>.Fail(
                $"The as-of date ({asOfDate:O}) must be on or after the start of fiscal year {year}.");
        }

        // 1. Opening balances: everything posted strictly before the beginning of
        //    the fiscal year (e.g. opening balances posted one minute before Jan 1).
        var openingRows = await GetAccountTotalsAsync(
            entries => entries.Where(j => j.PostedAt < periodStart), cancellationToken);

        // 2. Period activity: everything posted within [periodStart, asOfDate].
        var periodRows = await GetAccountTotalsAsync(
            entries => entries.Where(j => j.PostedAt >= periodStart && j.PostedAt <= asOfDate),
            cancellationToken);

        var openingByAccount = openingRows.ToDictionary(r => r.AccountId);
        var periodByAccount = periodRows.ToDictionary(r => r.AccountId);

        // 3. Every account (optionally active-only), ordered by code for a stable report.
        var accountsQuery = _dbContext.Accounts.AsNoTracking().AsQueryable();
        if (!includeInactiveAccounts)
        {
            accountsQuery = accountsQuery.Where(a => a.IsActive);
        }

        var accounts = await accountsQuery
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var lines = new List<TrialBalanceLineResponse>(accounts.Count);
        foreach (var account in accounts)
        {
            var openingDebit = 0m;
            var openingCredit = 0m;
            if (openingByAccount.TryGetValue(account.Id, out var opening))
            {
                openingDebit = opening.Debit;
                openingCredit = opening.Credit;
            }

            var periodDebit = 0m;
            var periodCredit = 0m;
            if (periodByAccount.TryGetValue(account.Id, out var period))
            {
                periodDebit = period.Debit;
                periodCredit = period.Credit;
            }

            // Only accounts with opening-balance or in-period activity are
            // included in the trial balance. An account whose opening and period
            // totals are both zero has no transactions to report and is omitted.
            if (openingDebit == 0m && openingCredit == 0m && periodDebit == 0m && periodCredit == 0m)
            {
                continue;
            }

            var closingDebit = openingDebit + periodDebit;
            var closingCredit = openingCredit + periodCredit;

            lines.Add(new TrialBalanceLineResponse
            {
                AccountId = account.Id,
                AccountCode = account.Code,
                AccountName = account.Name,
                AccountType = account.Type,
                OpeningDebit = openingDebit,
                OpeningCredit = openingCredit,
                OpeningBalance = Normalize(account.Type, openingDebit, openingCredit),
                PeriodDebit = periodDebit,
                PeriodCredit = periodCredit,
                PeriodBalance = Normalize(account.Type, periodDebit, periodCredit),
                ClosingDebit = closingDebit,
                ClosingCredit = closingCredit,
                ClosingBalance = Normalize(account.Type, closingDebit, closingCredit)
            });
        }

        var totalDebits = lines.Sum(l => l.ClosingDebit);
        var totalCredits = lines.Sum(l => l.ClosingCredit);

        return ServiceResult<TrialBalanceResponse>.Ok(new TrialBalanceResponse
        {
            FiscalYear = year,
            PeriodStart = periodStart,
            AsOf = asOfDate,
            IncludeInactiveAccounts = includeInactiveAccounts,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits,
            IsBalanced = totalDebits == totalCredits,
            Lines = lines
        });
    }

    /// <summary>
    /// Aggregates Debit/Credit totals per account for the journal entries selected
    /// by the supplied date filter.
    /// </summary>
    /// <remarks>
    /// The date window is applied over a lightweight in-memory index of journal
    /// entry Ids and post dates, because the SQLite provider (used by the
    /// service-level tests) cannot translate DateTimeOffset comparisons to SQL.
    /// The heavy per-account aggregation over the entry lines still runs on the
    /// database as a GROUP BY + SUM, so Postgres production queries stay efficient.
    /// </remarks>
    private async Task<List<(Guid AccountId, decimal Debit, decimal Credit)>> GetAccountTotalsAsync(
        Func<IEnumerable<(Guid Id, DateTimeOffset PostedAt)>, IEnumerable<(Guid Id, DateTimeOffset PostedAt)>> applyDateFilter,
        CancellationToken cancellationToken)
    {
        var index = await _dbContext.JournalEntries.AsNoTracking()
            .Select(j => new { j.Id, j.PostedAt })
            .ToListAsync(cancellationToken);

        var entryIds = applyDateFilter(index.Select(e => (e.Id, e.PostedAt)))
            .Select(e => e.Id)
            .ToList();

        if (entryIds.Count == 0)
        {
            return new List<(Guid AccountId, decimal Debit, decimal Credit)>();
        }

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

        return rows
            .Select(r => (r.AccountId, r.Debit, r.Credit))
            .ToList();
    }

    /// <summary>
    /// Returns the balance on the account type's natural side
    /// (Asset/Expense → Debit − Credit; Liability/Equity/Revenue → Credit − Debit).
    /// </summary>
    private static decimal Normalize(AccountType accountType, decimal debit, decimal credit) =>
        accountType.GetNormalBalance() == PostingType.Debit ? debit - credit : credit - debit;
}