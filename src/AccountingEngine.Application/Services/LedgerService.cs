using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

public class LedgerService : ILedgerService
{
    private readonly IAccountingDbContext _dbContext;

    public LedgerService(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<LedgerStatementResponse>> GetAccountStatementAsync(
        string accountCode,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            return ServiceResult<LedgerStatementResponse>.Fail("Account code is required.");
        }

        var cleanCode = accountCode.Trim();
        var account = await _dbContext.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code.ToLower() == cleanCode.ToLower(), cancellationToken);

        if (account is null)
        {
            return ServiceResult<LedgerStatementResponse>.Fail(
                $"Account with code '{cleanCode}' was not found.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return ServiceResult<LedgerStatementResponse>.Fail(
                "The 'from' date must be on or before the 'to' date.");
        }

        var toDate = to ?? DateTimeOffset.UtcNow;

        // Snapshot of journal entry dates so the window can be applied in memory:
        // the SQLite provider used by the service-level tests cannot translate
        // DateTimeOffset comparisons to SQL. The heavy lifting (loading the actual
        // lines for the account) still runs against the database.
        var entriesIndex = await _dbContext.JournalEntries.AsNoTracking()
            .Select(j => new { j.Id, j.PostedAt })
            .ToListAsync(cancellationToken);

        var windowIds = entriesIndex
            .Where(e => (!from.HasValue || e.PostedAt >= from.Value) && e.PostedAt <= toDate)
            .Select(e => e.Id)
            .ToList();

        var lines = await _dbContext.JournalEntryLines.AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == account.Id && windowIds.Contains(l.JournalEntryId))
            .ToListAsync(cancellationToken);

        var isDebitNormal = account.Type.GetNormalBalance() == PostingType.Debit;

        // Beginning balance: postings strictly before the From date (zero when no
        // lower bound is supplied, i.e. the statement starts at the first posting).
        var beginningLines = from.HasValue
            ? lines.Where(l => l.JournalEntry.PostedAt < from.Value).ToList()
            : new List<JournalEntryLine>();

        var beginningDebit = beginningLines.Sum(l => l.Debit);
        var beginningCredit = beginningLines.Sum(l => l.Credit);
        var beginningBalance = isDebitNormal
            ? beginningDebit - beginningCredit
            : beginningCredit - beginningDebit;

        var ordered = lines
            .Except(beginningLines)
            .OrderBy(l => l.JournalEntry.PostedAt)
            .ThenBy(l => l.Sequence)
            .ToList();

        var resultLines = new List<LedgerLineResponse>(ordered.Count);
        var running = beginningBalance;

        foreach (var line in ordered)
        {
            running = isDebitNormal
                ? running + line.Debit - line.Credit
                : running + line.Credit - line.Debit;

            resultLines.Add(new LedgerLineResponse
            {
                Id = line.Id,
                JournalEntryId = line.JournalEntryId,
                Reference = line.JournalEntry.Reference,
                SourceType = line.JournalEntry.SourceType,
                Description = line.Description ?? line.JournalEntry.Description,
                PostedAt = line.JournalEntry.PostedAt,
                Debit = line.Debit,
                Credit = line.Credit,
                Balance = running,
                Sequence = line.Sequence
            });
        }

        var totalDebit = ordered.Sum(l => l.Debit);
        var totalCredit = ordered.Sum(l => l.Credit);
        var endingBalance = resultLines.Count > 0 ? resultLines[^1].Balance : beginningBalance;

        return ServiceResult<LedgerStatementResponse>.Ok(new LedgerStatementResponse
        {
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.Name,
            AccountType = account.Type,
            IsActive = account.IsActive,
            From = from,
            To = toDate,
            BeginningBalance = beginningBalance,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            EndingBalance = endingBalance,
            Lines = resultLines
        });
    }
}