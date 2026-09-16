using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;
public class SourceRuleService : ISourceRuleService
{
    private readonly IAccountingDbContext _context;
    public SourceRuleService(IAccountingDbContext context)
    {
        _context = context;
    }
    public async Task<ServiceResult<SourceRuleResponse>> CreateRuleAsync(
        CreateSourceRuleRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ServiceResult<SourceRuleResponse>.Fail("Source rule request cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceType)
            || string.IsNullOrWhiteSpace(request.Description))
        {
            return ServiceResult<SourceRuleResponse>.Fail("Source type and description are required.");
        }

        var normalizedCode = (request.SourceType ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ServiceResult<SourceRuleResponse>.Fail("Source type and description are required.");
        }

        var createLines = request.RuleLines ?? new List<CreateSourceRuleLineRequest>();

        // 1. Check uniqueness
        var exists = await _context.SourceRules
            .AnyAsync(r => r.SourceType == normalizedCode, cancellationToken);

        if (exists)
        {
            return ServiceResult<SourceRuleResponse>.Fail(
                $"A source rule with code '{normalizedCode}' already exists.");
        }

        // 2. Validate rule configuration based on entry type
        if (request.IsManualEntryAllowed)
        {
            // For manual header-only rules, ignore/disallow template lines
            if (createLines.Any())
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    "Manual source rules must be header-only and cannot contain template lines.");
            }
        }
        else
        {
            // Automated rules MUST have at least two template lines (1 Debit, 1 Credit)
            if (createLines.Count < 2)
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    "Automated source rules must define at least two template lines.");
            }

            // Reject unknown posting sides before the debit/credit presence check so
            // callers get "Invalid posting side ..." instead of a misleading message.
            foreach (var line in createLines)
            {
                if (line is null
                    || string.IsNullOrWhiteSpace(line.AccountCode)
                    || !Enum.TryParse<PostingType>(line.EntryType?.Trim(), ignoreCase: true, out _))
                {
                    return ServiceResult<SourceRuleResponse>.Fail(
                        $"Invalid posting side '{line?.EntryType}' for account '{line?.AccountCode}'. Expected 'Debit' or 'Credit'.");
                }
            }

            var hasDebit = createLines.Any(l => (l.EntryType ?? string.Empty).Trim().Equals("Debit", StringComparison.OrdinalIgnoreCase));
            var hasCredit = createLines.Any(l => (l.EntryType ?? string.Empty).Trim().Equals("Credit", StringComparison.OrdinalIgnoreCase));

            if (!hasDebit || !hasCredit)
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    "Automated source rules must contain at least one Debit line and one Credit line.");
            }

            // Verify that referenced accounts exist and are active
            var accountCodes = createLines.Select(l => (l.AccountCode ?? string.Empty).Trim()).Distinct().ToList();
            var existingAccountCodes = await _context.Accounts
                .Where(a => accountCodes.Contains(a.Code) && a.IsActive)
                .Select(a => a.Code)
                .ToListAsync(cancellationToken);

            var missingAccounts = accountCodes.Except(existingAccountCodes).ToList();
            if (missingAccounts.Any())
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    $"Referenced accounts do not exist or are inactive: {string.Join(", ", missingAccounts)}");
            }
        }

        // 3. Build domain entity
        var rule = new SourceRule
        {
            Id = Guid.NewGuid(),
            SourceType = normalizedCode,
            Description = request.Description.Trim(),
            IsActive = true,
            IsManualEntryAllowed = request.IsManualEntryAllowed,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // 4. Map automated rule lines if applicable
        if (!request.IsManualEntryAllowed)
        {
            var accountCodes = createLines.Select(l => (l.AccountCode ?? string.Empty).Trim()).Distinct().ToList();
            var accountMap = await _context.Accounts
                .Where(a => accountCodes.Contains(a.Code))
                .ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);

            foreach (var lineReq in createLines)
            {
                var cleanCode = (lineReq.AccountCode ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(lineReq.EntryType)
                    || !Enum.TryParse<PostingType>(lineReq.EntryType.Trim(), ignoreCase: true, out var entryType))
                {
                    return ServiceResult<SourceRuleResponse>.Fail(
                        $"Invalid posting side '{lineReq.EntryType}' for account '{cleanCode}'. Expected 'Debit' or 'Credit'.");
                }

                if (string.IsNullOrWhiteSpace(lineReq.AmountType))
                {
                    return ServiceResult<SourceRuleResponse>.Fail(
                        $"Amount type identifier is required for account '{cleanCode}' (e.g., BASE_AMOUNT, TAX_AMOUNT, TOTAL_AMOUNT).");
                }

                rule.RuleLines.Add(new SourceRuleLine
                {
                    Id = Guid.NewGuid(),
                    SourceRuleId = rule.Id,
                    AccountId = accountMap[cleanCode],
                    EntryType = entryType,
                    AmountType = lineReq.AmountType.Trim().ToUpperInvariant(),
                    Sequence = lineReq.Sequence
                });
            }
        }

        _context.SourceRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        // Fetch back rule including Account navigation for response mapping
        var savedRule = await _context.SourceRules
            .Include(r => r.RuleLines)
                .ThenInclude(l => l.Account)
            .FirstAsync(r => r.Id == rule.Id, cancellationToken);

        return ServiceResult<SourceRuleResponse>.Ok(MapToResponse(savedRule));
    }

    public async Task<List<SourceRuleResponse>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _context.SourceRules
            .Include(r => r.RuleLines)
                .ThenInclude(l => l.Account)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rules.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResult<SourceRuleResponse>> UpdateRuleAsync(
        string sourceType,
        UpdateSourceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return ServiceResult<SourceRuleResponse>.Fail($"Source rule '{sourceType}' not found.");

        var normalizedSourceType = (sourceType ?? string.Empty).Trim().ToUpperInvariant();
        var rule = await _context.SourceRules
            .Include(r => r.RuleLines)
            .FirstOrDefaultAsync(r => r.SourceType == normalizedSourceType, cancellationToken);

        if (rule is null)
            return ServiceResult<SourceRuleResponse>.Fail($"Source rule '{sourceType}' not found.");
        return await ApplyRuleUpdateAsync(rule, request, cancellationToken);
    }

    /// <summary>
    /// Updates a rule located by its stable Id. Renaming sourceType is safe
    /// here because the Id (not the mutable code) identifies the row, so
    /// XXX -&gt; XXX_UPDATED can never 404 or hit the wrong row.
    /// </summary>
    public async Task<ServiceResult<SourceRuleResponse>> UpdateRuleByIdAsync(
        Guid id,
        UpdateSourceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var rule = await _context.SourceRules
            .Include(r => r.RuleLines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule is null)
            return ServiceResult<SourceRuleResponse>.Fail($"Source rule '{id}' not found.");

        return await ApplyRuleUpdateAsync(rule, request, cancellationToken);
    }

    /// <summary>Shared update core for both keyed lookups above.</summary>
    private async Task<ServiceResult<SourceRuleResponse>> ApplyRuleUpdateAsync(
        SourceRule rule,
        UpdateSourceRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return ServiceResult<SourceRuleResponse>.Fail("Source rule request cannot be null.");

        var isUsed = await _context.JournalEntries
            .AnyAsync(j => j.SourceType == rule.SourceType, cancellationToken);

        if (isUsed)
            return ServiceResult<SourceRuleResponse>.Fail(
                $"Source rule '{rule.SourceType}' cannot be modified because it has been used by journal entries.");

        if (string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.Description))
            return ServiceResult<SourceRuleResponse>.Fail("Source type and description are required.");

        var newSourceType = (request.SourceType ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(newSourceType))
            return ServiceResult<SourceRuleResponse>.Fail("Source type and description are required.");

        var duplicate = await _context.SourceRules
            .AnyAsync(r => r.Id != rule.Id && r.SourceType == newSourceType, cancellationToken);
        if (duplicate)
            return ServiceResult<SourceRuleResponse>.Fail(
                $"A source rule with code '{newSourceType}' already exists.");

        var updateLines = request.RuleLines ?? new List<CreateSourceRuleLineRequest>();

        var validationError = await ValidateRuleRequestAsync(
            new CreateSourceRuleRequest
            {
                SourceType = newSourceType,
                Description = request.Description,
                IsManualEntryAllowed = request.IsManualEntryAllowed,
                RuleLines = updateLines
            }, cancellationToken);

        if (validationError is not null)
            return ServiceResult<SourceRuleResponse>.Fail(validationError);

        var accountCodes = updateLines
            .Select(l => (l?.AccountCode ?? string.Empty).Trim())
            .Distinct()
            .ToList();

        var accountMap = await _context.Accounts
            .Where(a => accountCodes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);

        // Replace the template lines. New lines MUST be registered through the
        // DbSet so the change tracker assigns them the Added state: when a
        // client-keyed entity is only appended to a tracked parent's navigation
        // after RemoveRange, change detection classifies it as Modified and
        // SaveChanges emits UPDATE ... WHERE id = <new Guid>, which matches 0
        // rows and throws a bogus DbUpdateConcurrencyException ("modified by
        // another request"). Snapshot the collection first because RemoveRange
        // fixes up (mutates) the navigation while it deletes.

        var originalLines = rule.RuleLines.ToList();

        _context.SourceRuleLines.RemoveRange(originalLines);

        rule.RuleLines.Clear();
        rule.SourceType = newSourceType;
        rule.Description = (request.Description ?? string.Empty).Trim();
        rule.IsManualEntryAllowed = request.IsManualEntryAllowed;
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        if (!request.IsManualEntryAllowed)
        {
            foreach (var lineRequest in updateLines)
            {
                Enum.TryParse<PostingType>((lineRequest.EntryType ?? string.Empty).Trim(), true, out var entryType);
                var line = new SourceRuleLine
                {
                    Id = Guid.NewGuid(),
                    SourceRuleId = rule.Id,
                    AccountId = accountMap[(lineRequest.AccountCode ?? string.Empty).Trim()],
                    EntryType = entryType,
                    AmountType = (lineRequest.AmountType ?? string.Empty).Trim().ToUpperInvariant(),
                    Sequence = lineRequest.Sequence
                };
                _context.SourceRuleLines.Add(line);
                rule.RuleLines.Add(line);
            }
        }
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<SourceRuleResponse>.Fail(
                $"Could not save source rule '{newSourceType}': it was modified or deleted by another request. Reload and try again.");
        }
        catch (DbUpdateException ex)
        {
            return ServiceResult<SourceRuleResponse>.Fail(
                $"Could not save source rule '{newSourceType}': {ex.GetBaseException().Message}");
        }

        // Re-read the committed state with no tracking: after the
        // delete-and-replace above, the tracked rule's RuleLines navigation can
        // hold stale references, and query fixup on a tracking re-read would
        // surface them (duplicated lines) in the response. A no-tracking read
        // reflects exactly what was committed.
        var savedRule = await _context.SourceRules
            .AsNoTracking()
            .Include(r => r.RuleLines)
                .ThenInclude(l => l.Account)
            .FirstAsync(r => r.Id == rule.Id, cancellationToken);

        return ServiceResult<SourceRuleResponse>.Ok(MapToResponse(savedRule));
    }

    public async Task<ServiceResult<bool>> DeleteRuleAsync(
        string sourceType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return ServiceResult<bool>.Fail($"Source rule '{sourceType}' not found.");

        var normalizedSourceType = (sourceType ?? string.Empty).Trim().ToUpperInvariant();
        var rule = await _context.SourceRules
            .FirstOrDefaultAsync(r => r.SourceType == normalizedSourceType, cancellationToken);

        if (rule is null)
            return ServiceResult<bool>.Fail($"Source rule '{sourceType}' not found.");

        return await DeleteRuleCoreAsync(rule, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeleteRuleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rule = await _context.SourceRules
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule is null)
            return ServiceResult<bool>.Fail($"Source rule '{id}' not found.");

        return await DeleteRuleCoreAsync(rule, cancellationToken);
    }

    /// <summary>
    /// Gets a single rule by its stable Id. Prefer this over sourceType
    /// lookups: SourceType is mutable (renameable) so it is not a stable
    /// resource key.
    /// </summary>
    public async Task<ServiceResult<SourceRuleResponse>> GetRuleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rule = await _context.SourceRules
            .Include(r => r.RuleLines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule is null)
            return ServiceResult<SourceRuleResponse>.Fail($"Source rule '{id}' not found.");

        return ServiceResult<SourceRuleResponse>.Ok(MapToResponse(rule));
    }

    /// <summary>Shared delete core for both keyed lookups above.</summary>
    private async Task<ServiceResult<bool>> DeleteRuleCoreAsync(
        SourceRule rule,
        CancellationToken cancellationToken)
    {
        var isUsed = await _context.JournalEntries
            .AnyAsync(j => j.SourceType == rule.SourceType, cancellationToken);
        if (isUsed)
            return ServiceResult<bool>.Fail(
                $"Source rule '{rule.SourceType}' cannot be deleted because it has been used by journal entries.");

        _context.SourceRules.Remove(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> ToggleActiveStatusAsync(
        string sourceType, 
        bool isActive, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return ServiceResult<bool>.Fail($"Source rule '{sourceType}' not found.");

        var rule = await _context.SourceRules
            .FirstOrDefaultAsync(r => r.SourceType == (sourceType ?? string.Empty).Trim().ToUpperInvariant(), cancellationToken);

        if (rule is null)
        {
            return ServiceResult<bool>.Fail($"Source rule '{sourceType}' not found.");
        }

        rule.IsActive = isActive;
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Well-known amount-type identifiers understood by the engine's
    /// automated posting (see the amount lookup in JournalService).
    /// AmountType is free-form in the database, so identifiers already used
    /// by existing rules are returned alongside these defaults.
    /// </summary>
    private static readonly string[] WellKnownAmountTypes =
    {
        "TOTAL_AMOUNT",
        "BASE_AMOUNT",
        "TAX_AMOUNT",
        "FREIGHT_AMOUNT",
        "DISCOUNT_AMOUNT",
        "NET_AMOUNT",
        "CUSTOM"
    };

    /// <inheritdoc />
    public async Task<List<string>> GetAmountTypesAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SourceRule> query = _context.SourceRules.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        var usedTypes = await query
            .SelectMany(r => r.RuleLines.Select(l => l.AmountType))
            .ToListAsync(cancellationToken);

        return usedTypes
            .Concat(WellKnownAmountTypes)
            .Select(a => (a ?? string.Empty).Trim().ToUpperInvariant())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string?> ValidateRuleRequestAsync(
        CreateSourceRuleRequest request,
        CancellationToken cancellationToken)
    {
        var lines = request.RuleLines ?? new List<CreateSourceRuleLineRequest>();

        if (request.IsManualEntryAllowed)
        {
            return lines.Any()
                ? "Manual source rules must be header-only and cannot contain template lines."
                : null;
        }

        if (lines.Count < 2)
            return "Automated source rules must define at least two template lines.";

        foreach (var line in lines)
        {
            if (line is null
                || string.IsNullOrWhiteSpace(line.AccountCode)
                || !Enum.TryParse<PostingType>(line.EntryType?.Trim(), true, out _))
                return $"Invalid posting side '{line?.EntryType}' for account '{line?.AccountCode}'. Expected 'Debit' or 'Credit'.";
        }

        var hasDebit = lines.Any(l => (l.EntryType ?? string.Empty).Trim().Equals("Debit", StringComparison.OrdinalIgnoreCase));
        var hasCredit = lines.Any(l => (l.EntryType ?? string.Empty).Trim().Equals("Credit", StringComparison.OrdinalIgnoreCase));
        if (!hasDebit || !hasCredit)
            return "Automated source rules must contain at least one Debit line and one Credit line.";

        var accountCodes = lines.Select(l => (l.AccountCode ?? string.Empty).Trim()).Distinct().ToList();
        var existingAccountCodes = await _context.Accounts
            .Where(a => accountCodes.Contains(a.Code) && a.IsActive)
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);

        var missingAccounts = accountCodes.Except(existingAccountCodes).ToList();
        if (missingAccounts.Any())
            return $"Referenced accounts do not exist or are inactive: {string.Join(", ", missingAccounts)}";

        foreach (var line in lines)
        {
            if (line is null || string.IsNullOrWhiteSpace(line.AmountType))
                return $"Amount type identifier is required for account '{line?.AccountCode}'.";
        }

        return null;
    }

    private static SourceRuleResponse MapToResponse(SourceRule rule) => new()
    {
        Id = rule.Id,
        SourceType = rule.SourceType,
        Description = rule.Description,
        IsActive = rule.IsActive,
        IsManualEntryAllowed = rule.IsManualEntryAllowed,
        RuleLines = rule.RuleLines.Select(l => new SourceRuleLineResponse
        {
            Id = l.Id,
            AccountCode = l.Account?.Code ?? string.Empty,
            EntryType = l.EntryType.ToString(),
            AmountType = l.AmountType,
            Sequence = l.Sequence
        }).ToList()
    };
}