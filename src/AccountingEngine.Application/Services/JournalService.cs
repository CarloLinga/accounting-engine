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

    public async Task<ServiceResult<JournalEntryResponse>> PostGeneralJournalAsync(
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
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Invalid transaction source '{normalizedSource}'. This SourceType is not defined in system rules.");
        }

        if (!rule.IsActive)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Transaction source '{normalizedSource}' is currently inactive.");
        }

        if (!rule.IsManualEntryAllowed)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Source type '{normalizedSource}' is reserved for automated postings and cannot be posted manually.");
        }

        // 2. Line count check
        if (request.Lines == null || request.Lines.Count < 2)
            return ServiceResult<JournalEntryResponse>.Fail("A journal entry must contain at least two lines.");

        // 3. Double-entry balance check
        var totalDebit = request.Lines.Sum(l => l.Debit);
        var totalCredit = request.Lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Unbalanced entry: Total Debits ({totalDebit:C2}) do not equal Total Credits ({totalCredit:C2}). Difference: {Math.Abs(totalDebit - totalCredit):C2}.");
        }

        if (totalDebit <= 0)
        {
            return ServiceResult<JournalEntryResponse>.Fail("Transaction total must be greater than zero.");
        }

        // 4. Resolve Accounts
        var accountCodes = request.Lines.Select(l => l.AccountCode.Trim()).Distinct().ToList();
        var accounts = await _dbContext.Accounts
            .Where(a => accountCodes.Contains(a.Code) && a.IsActive)
            .ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);

        var missingAccounts = accountCodes.Except(accounts.Keys).ToList();
        if (missingAccounts.Any())
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"The following account codes do not exist or are inactive: {string.Join(", ", missingAccounts)}");
        }

        // 5. Line-level validation
        for (int i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if ((line.Debit > 0 && line.Credit > 0) || (line.Debit == 0 && line.Credit == 0))
            {
                return ServiceResult<JournalEntryResponse>.Fail(
                    $"Line {i + 1} for account '{line.AccountCode}' is invalid: must specify either Debit OR Credit, but not both or neither.");
            }
        }

        // 6. Create Journal Entry Entity
        var journalEntry = new JournalEntry
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
                JournalEntryId = journalEntry.Id,
                AccountId = accounts[cleanCode],
                Sequence = lineReq.Sequence,
                Debit = lineReq.Debit,
                Credit = lineReq.Credit,
                Description = lineReq.Description?.Trim()
            };

            _dbContext.JournalEntryLines.Add(line);
        }

        // 8. Persist
        _dbContext.JournalEntries.Add(journalEntry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<JournalEntryResponse>.Ok(new JournalEntryResponse
        {
            Id = journalEntry.Id,
            SourceType = journalEntry.SourceType,
            Reference = journalEntry.Reference,
            Description = journalEntry.Description,
            PostedAt = journalEntry.PostedAt,
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

    public async Task<List<JournalEntryResponse>> GetJournalEntriesAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        string? sourceType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.JournalEntries
            .Include(t => t.JournalEntryLines)
                .ThenInclude(l => l.Account)
            .AsNoTracking()
            .AsQueryable();

        // Apply filters dynamically
        if (startDate.HasValue)
        {
            query = query.Where(t => t.PostedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.PostedAt <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(sourceType))
        {
            var cleanSourceType = sourceType.Trim().ToUpperInvariant();
            query = query.Where(t => t.SourceType == cleanSourceType);
        }

        var journalEntryLines = await query
            .OrderByDescending(t => t.PostedAt)
            .ToListAsync(cancellationToken);

        return journalEntryLines.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResult<JournalEntryResponse>> GetJournalEntryByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default)
    {
        var journalEntry = await _dbContext.JournalEntries
            .Include(t => t.JournalEntryLines)
                .ThenInclude(l => l.Account)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (journalEntry is null)
        {
            return ServiceResult<JournalEntryResponse>.Fail($"Journal Entry with ID '{id}' was not found.");
        }

        return ServiceResult<JournalEntryResponse>.Ok(MapToResponse(journalEntry));
    }
    
    public async Task<JournalEntryResponse?> GetJournalEntryByReferenceAsync(
        string reference, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var cleanRef = reference.Trim();

        var journalEntry = await _dbContext.JournalEntries
            .AsNoTracking()
            .Include(t => t.JournalEntryLines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(t => EF.Functions.Like(t.Reference, cleanRef), cancellationToken);

        if (journalEntry is null) 
            return null;

        return new JournalEntryResponse
        {
            Id = journalEntry.Id,
            SourceType = journalEntry.SourceType,
            Reference = journalEntry.Reference,
            Description = journalEntry.Description,
            PostedAt = journalEntry.PostedAt,
            Lines = journalEntry.JournalEntryLines
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

    private static JournalEntryResponse MapToResponse(JournalEntry t) => new()
    {
        Id = t.Id,
        Reference = t.Reference,
        SourceType = t.SourceType,
        Description = t.Description,
        PostedAt = t.PostedAt,
        Lines = t.JournalEntryLines
            .OrderBy(l => l.Sequence)
            .Select(l => new JournalLineResponse
            {
                Id = l.Id,
                AccountCode = l.Account?.Code ?? string.Empty,
                AccountName = l.Account?.Name ?? string.Empty,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description,
                Sequence = l.Sequence
            }).ToList()
    };
}
