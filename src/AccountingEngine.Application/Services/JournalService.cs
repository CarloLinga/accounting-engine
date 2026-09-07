using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

public class JournalService : IJournalService
{
    private readonly IAccountingDbContext _dbContext;

    public JournalService(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<TransactionResponse>> PostGeneralJournalAsync(
        PostGeneralJournalRequest request, 
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = request.SourceType.Trim().ToUpperInvariant();

        // 1. Validate SourceType against EventRules table
        var rule = await _dbContext.SourceRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SourceType == normalizedSource, cancellationToken);

        if (rule is null)
        {
            return ServiceResult<TransactionResponse>.Fail(
                $"Invalid transaction source '{normalizedSource}'. This SourceType is not defined in system rules.");
        }

        if (!rule.IsActive)
        {
            return ServiceResult<TransactionResponse>.Fail(
                $"Transaction source '{normalizedSource}' is currently inactive.");
        }

        if (!rule.IsManualEntryAllowed)
        {
            return ServiceResult<TransactionResponse>.Fail(
                $"Source type '{normalizedSource}' is reserved for automated postings and cannot be posted manually.");
        }

        // 2. Line count check
        if (request.Lines == null || request.Lines.Count < 2)
            return ServiceResult<TransactionResponse>.Fail("A journal entry must contain at least two lines.");

        // 3. Double-entry balance check
        var totalDebit = request.Lines.Sum(l => l.Debit);
        var totalCredit = request.Lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
        {
            return ServiceResult<TransactionResponse>.Fail(
                $"Unbalanced entry: Total Debits ({totalDebit:C2}) do not equal Total Credits ({totalCredit:C2}). Difference: {Math.Abs(totalDebit - totalCredit):C2}.");
        }

        if (totalDebit <= 0)
        {
            return ServiceResult<TransactionResponse>.Fail("Transaction total must be greater than zero.");
        }

        // 4. Resolve Accounts
        var accountCodes = request.Lines.Select(l => l.AccountCode.Trim()).Distinct().ToList();
        var accounts = await _dbContext.Accounts
            .Where(a => accountCodes.Contains(a.Code) && a.IsActive)
            .ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);

        var missingAccounts = accountCodes.Except(accounts.Keys).ToList();
        if (missingAccounts.Any())
        {
            return ServiceResult<TransactionResponse>.Fail(
                $"The following account codes do not exist or are inactive: {string.Join(", ", missingAccounts)}");
        }

        // 5. Line-level validation
        for (int i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if ((line.Debit > 0 && line.Credit > 0) || (line.Debit == 0 && line.Credit == 0))
            {
                return ServiceResult<TransactionResponse>.Fail(
                    $"Line {i + 1} for account '{line.AccountCode}' is invalid: must specify either Debit OR Credit, but not both or neither.");
            }
        }

        // 6. Create Transaction Entity
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            SourceType = normalizedSource,
            Reference = request.Reference.Trim(),
            Description = request.Description?.Trim(),
            PostedAt = request.PostedAt,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // 7. Build Journal Lines
        foreach (var lineReq in request.Lines)
        {
            var cleanCode = lineReq.AccountCode.Trim();

            var line = new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                AccountId = accounts[cleanCode],
                Sequence = lineReq.Sequence,
                Debit = lineReq.Debit,
                Credit = lineReq.Credit,
                Description = lineReq.Description?.Trim()
            };

            _dbContext.JournalEntryLines.Add(line);
        }

        // 8. Persist
        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<TransactionResponse>.Ok(new TransactionResponse
        {
            Id = transaction.Id,
            SourceType = transaction.SourceType,
            Reference = transaction.Reference,
            Description = transaction.Description,
            PostedAt = transaction.PostedAt,
            Lines = request.Lines.Select((l, index) => new JournalLineResponse
            {
                Id = Guid.Empty,
                Sequence = l.Sequence == 0 ? index + 1 : l.Sequence,
                AccountCode = l.AccountCode,
                AccountName = accounts.ContainsKey(l.AccountCode.Trim()) ? l.AccountCode.Trim() : string.Empty,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            }).ToList()
        });
    }

    public async Task<TransactionResponse?> GetTransactionByReferenceAsync(
        string reference, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var cleanRef = reference.Trim();

        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .Include(t => t.JournalEntryLines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(t => EF.Functions.Like(t.Reference, cleanRef), cancellationToken);

        if (transaction is null) 
            return null;

        return new TransactionResponse
        {
            Id = transaction.Id,
            SourceType = transaction.SourceType,
            Reference = transaction.Reference,
            Description = transaction.Description,
            PostedAt = transaction.PostedAt,
            Lines = transaction.JournalEntryLines
                .OrderBy(l => l.Sequence)
                .Select(l => new JournalLineResponse
                {
                    Id = l.Id,
                    Sequence = l.Sequence,
                    AccountCode = l.Account.Code,
                    AccountName = l.Account.Name,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Description = l.Description
                })
                .ToList()
        };
    }
}
