using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

public class JournalService : IJournalService
{
    private readonly IAccountingDbContext _dbContext;

    public JournalService(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<JournalEntryResponse>> PostJournalEntryAsync(
        PostGeneralJournalRequest request, 
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = request.SourceType.Trim().ToUpperInvariant();

        // 1. Validate SourceType against SourceRules table
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
        var accountsByCode = await _dbContext.Accounts
            .Where(a => accountCodes.Contains(a.Code) && a.IsActive)
            .ToDictionaryAsync(a => a.Code, a => a, cancellationToken);

        var missingAccounts = accountCodes.Except(accountsByCode.Keys).ToList();
        if (missingAccounts.Any())
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"The following account codes do not exist or are inactive: {string.Join(", ", missingAccounts)}");
        }

        // Headers (IsPostable=false) carry no balances; reject direct postings.
        var headerCodes = accountsByCode.Values
            .Where(a => !a.IsPostable)
            .Select(a => a.Code)
            .ToList();
        if (headerCodes.Any())
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"The following accounts are report headers and cannot be posted to directly: {string.Join(", ", headerCodes)}");
        }

        var accounts = accountsByCode.ToDictionary(kv => kv.Key, kv => kv.Value.Id);

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

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"A journal entry with reference '{journalEntry.Reference}' already exists.");
        }

        return ServiceResult<JournalEntryResponse>.Ok(new JournalEntryResponse
        {
            Id = journalEntry.Id,
            SourceType = journalEntry.SourceType,
            Reference = journalEntry.Reference,
            Description = journalEntry.Description,
            PostedAt = journalEntry.PostedAt,
            JournalLines = request.Lines.Select((l, index) => new JournalLineResponse
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
            query = query.Where(t => t.SourceType.Trim().ToUpper() == cleanSourceType);
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
            JournalLines = journalEntry.JournalEntryLines
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

    public async Task<ServiceResult<JournalEntryResponse>> UpdateJournalEntryAsync(
        Guid id,
        UpdateJournalEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return ServiceResult<JournalEntryResponse>.Fail("Journal update request cannot be null.");

        var entry = await _dbContext.JournalEntries
            .Include(j => j.JournalEntryLines)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (entry is null)
            return ServiceResult<JournalEntryResponse>.Fail($"Journal Entry with ID '{id}' was not found.");

        var cleanReference = request.Reference?.Trim() ?? string.Empty;
        if (cleanReference.Length == 0)
            return ServiceResult<JournalEntryResponse>.Fail("Transaction reference is required.");

        var duplicateReference = await _dbContext.JournalEntries
            .AnyAsync(j => j.Id != id && j.Reference == cleanReference, cancellationToken);
        if (duplicateReference)
            return ServiceResult<JournalEntryResponse>.Fail(
                $"A journal entry with reference '{cleanReference}' already exists.");

        var validation = await ValidateJournalLinesAsync(request.Lines, cancellationToken);
        if (!validation.Success)
            return ServiceResult<JournalEntryResponse>.Fail(validation.ErrorMessage!);

        _dbContext.JournalEntryLines.RemoveRange(entry.JournalEntryLines);
        entry.JournalEntryLines.Clear();
        entry.Reference = cleanReference;
        entry.Description = request.Description?.Trim();
        entry.PostedAt = request.PostedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var lineRequest in request.Lines)
        {
            var accountCode = lineRequest.AccountCode.Trim();
            _dbContext.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = validation.Data![accountCode],
                Sequence = lineRequest.Sequence,
                Debit = lineRequest.Debit,
                Credit = lineRequest.Credit,
                Description = lineRequest.Description?.Trim()
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedEntry = await _dbContext.JournalEntries
            .Include(j => j.JournalEntryLines)
                .ThenInclude(l => l.Account)
            .FirstAsync(j => j.Id == id, cancellationToken);

        return ServiceResult<JournalEntryResponse>.Ok(MapToResponse(savedEntry));
    }

    public async Task<ServiceResult<bool>> DeleteJournalEntryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.JournalEntries
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (entry is null)
            return ServiceResult<bool>.Fail($"Journal Entry with ID '{id}' was not found.");

        _dbContext.JournalEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    private async Task<ServiceResult<Dictionary<string, Guid>>> ValidateJournalLinesAsync(
        List<JournalLineRequest>? lines,
        CancellationToken cancellationToken)
    {
        if (lines is null || lines.Count < 2)
            return ServiceResult<Dictionary<string, Guid>>.Fail("A journal entry must contain at least two lines.");

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
            return ServiceResult<Dictionary<string, Guid>>.Fail(
                $"Unbalanced entry: Total Debits ({totalDebit:C2}) do not equal Total Credits ({totalCredit:C2}). Difference: {Math.Abs(totalDebit - totalCredit):C2}.");

        if (totalDebit <= 0)
            return ServiceResult<Dictionary<string, Guid>>.Fail("Transaction total must be greater than zero.");

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.AccountCode))
                return ServiceResult<Dictionary<string, Guid>>.Fail($"Account code is required for line {i + 1}.");

            if ((line.Debit > 0 && line.Credit > 0) || (line.Debit == 0 && line.Credit == 0))
                return ServiceResult<Dictionary<string, Guid>>.Fail(
                    $"Line {i + 1} for account '{line.AccountCode}' is invalid: must specify either Debit OR Credit, but not both or neither.");
        }

        var accountCodes = lines.Select(l => l.AccountCode.Trim()).Distinct().ToList();
        var accounts = await _dbContext.Accounts
            .Where(a => accountCodes.Contains(a.Code) && a.IsActive && a.IsPostable)
            .ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);

        var missingAccounts = accountCodes.Except(accounts.Keys).ToList();
        if (missingAccounts.Count > 0)
            return ServiceResult<Dictionary<string, Guid>>.Fail(
                $"The following account codes do not exist, are inactive, or are report headers: {string.Join(", ", missingAccounts)}");

        return ServiceResult<Dictionary<string, Guid>>.Ok(accounts);
    }

    private static JournalEntryResponse MapToResponse(JournalEntry t) => new()
    {
        Id = t.Id,
        Reference = t.Reference,
        SourceType = t.SourceType,
        Description = t.Description,
        PostedAt = t.PostedAt,
        JournalLines = t.JournalEntryLines
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

    public async Task<ServiceResult<JournalEntryResponse>> PostJournalSourceAsync(
        PostSourceTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ServiceResult<JournalEntryResponse>.Fail("Source transaction request cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceType))
        {
            return ServiceResult<JournalEntryResponse>.Fail("Source type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return ServiceResult<JournalEntryResponse>.Fail("Transaction reference is required.");
        }

        if (request.Amounts is null || request.Amounts.Count == 0)
        {
            return ServiceResult<JournalEntryResponse>.Fail("At least one amount must be provided.");
        }

        var normalizedSource = request.SourceType.Trim().ToUpperInvariant();
        var cleanReference = request.Reference.Trim();

        // 1. Fetch SourceRule with lines AND accounts in one graph, then detach the
        // whole graph so subsequent reads/creates aren't affected by change tracking.
        var rule = await _dbContext.SourceRules
            .Include(r => r.RuleLines)
            .ThenInclude(l => l.Account)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SourceType == normalizedSource, cancellationToken);

        if (rule is null)
        {
            return ServiceResult<JournalEntryResponse>.Fail($"No Source Rule found for '{normalizedSource}'.");
        }

        if (!rule.IsActive)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Transaction source '{normalizedSource}' is currently inactive.");
        }

        if (rule.IsManualEntryAllowed)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Source type '{normalizedSource}' is configured for manual entries and cannot be posted automatically.");
        }

        if (rule.RuleLines.Count == 0)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Source rule '{normalizedSource}' has no template lines configured.");
        }

        var duplicateReference = await _dbContext.JournalEntries
            .AnyAsync(j => j.Reference == cleanReference, cancellationToken);

        if (duplicateReference)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"A journal entry with reference '{cleanReference}' already exists.");
        }

        // Normalize incoming amount keys so lookups stay case-insensitive even for
        // DbContext implementations that do not honour the dictionary comparer.
        var amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in request.Amounts)
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return ServiceResult<JournalEntryResponse>.Fail("Amount keys cannot be empty.");
            }

            amounts[key] = kvp.Value;
        }

        var journalLines = new List<JournalEntryLine>();
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        // 2. Evaluate each rule line against provided amounts.
        foreach (var lineRule in rule.RuleLines.OrderBy(l => l.Sequence))
        {
            if (!amounts.TryGetValue(lineRule.AmountType, out var amount) || amount <= 0)
            {
                return ServiceResult<JournalEntryResponse>.Fail(
                    $"Missing or invalid amount for key '{lineRule.AmountType}'.");
            }

            var account = lineRule.Account
                ?? await _dbContext.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == lineRule.AccountId, cancellationToken);

            if (account is null)
            {
                return ServiceResult<JournalEntryResponse>.Fail($"Account ID '{lineRule.AccountId}' not found.");
            }

            if (!account.IsActive)
            {
                return ServiceResult<JournalEntryResponse>.Fail(
                    $"Account '{account.Code}' referenced by source rule '{normalizedSource}' is inactive.");
            }

            if (!account.IsPostable)
            {
                return ServiceResult<JournalEntryResponse>.Fail(
                    $"Account '{account.Code}' referenced by source rule '{normalizedSource}' is a report header and cannot be posted to.");
            }

            var isDebit = lineRule.EntryType == PostingType.Debit;
            var debit = isDebit ? amount : 0m;
            var credit = isDebit ? 0m : amount;

            totalDebit += debit;
            totalCredit += credit;

            journalLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Debit = debit,
                Credit = credit,
                Sequence = lineRule.Sequence,
                Description = (request.Description ?? rule.Description)?.Trim()
            });
        }

        // 3. Enforce Double-Entry Balancing Principle
        if (totalDebit != totalCredit)
        {
            return ServiceResult<JournalEntryResponse>.Fail(
                $"Transaction is unbalanced. Total Debits ({totalDebit}) != Total Credits ({totalCredit}).");
        }

        // 4. Construct JournalEntry Header
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            Reference = cleanReference,
            SourceType = normalizedSource,
            Description = (request.Description ?? rule.Description)?.Trim(),
            PostedAt = request.PostedAt,
            CreatedAt = DateTimeOffset.UtcNow,
            JournalEntryLines = journalLines
        };

        _dbContext.JournalEntries.Add(entry);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;

            if (detail.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<JournalEntryResponse>.Fail(
                    $"A journal entry with reference '{cleanReference}' already exists.");
            }

            return ServiceResult<JournalEntryResponse>.Fail($"Failed to save journal entry '{cleanReference}': {detail}");
        }

        // Reload with accounts so the response includes account codes/names.
        var savedEntry = await _dbContext.JournalEntries
            .Include(j => j.JournalEntryLines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.Id == entry.Id, cancellationToken);

        return ServiceResult<JournalEntryResponse>.Ok(MapToResponse(savedEntry ?? entry));
    }
}
