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
        var normalizedCode = request.SourceType.Trim().ToUpperInvariant();

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
            if (request.RuleLines.Any())
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    "Manual source rules must be header-only and cannot contain template lines.");
            }
        }
        else
        {
            // Automated rules MUST have at least two template lines (1 Debit, 1 Credit)
            if (request.RuleLines.Count < 2)
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    "Automated source rules must define at least two template lines.");
            }

            var hasDebit = request.RuleLines.Any(l => l.EntryType.Equals("Debit", StringComparison.OrdinalIgnoreCase));
            var hasCredit = request.RuleLines.Any(l => l.EntryType.Equals("Credit", StringComparison.OrdinalIgnoreCase));

            if (!hasDebit || !hasCredit)
            {
                return ServiceResult<SourceRuleResponse>.Fail(
                    "Automated source rules must contain at least one Debit line and one Credit line.");
            }

            // Verify that referenced accounts exist and are active
            var accountCodes = request.RuleLines.Select(l => l.AccountCode.Trim()).Distinct().ToList();
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
            var accountCodes = request.RuleLines.Select(l => l.AccountCode.Trim()).Distinct().ToList();
            var accountMap = await _context.Accounts
                .Where(a => accountCodes.Contains(a.Code))
                .ToDictionaryAsync(a => a.Code, a => a.Id, cancellationToken);

            foreach (var lineReq in request.RuleLines)
            {
                var cleanCode = lineReq.AccountCode.Trim();

                rule.RuleLines.Add(new SourceRuleLine
                {
                    Id = Guid.NewGuid(),
                    SourceRuleId = rule.Id,
                    AccountId = accountMap[cleanCode],
                    EntryType = Enum.Parse<PostingType>(lineReq.EntryType.Trim(), ignoreCase: true),
                    AmountType = lineReq.AmountType.Trim(),
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

    public async Task<ServiceResult<bool>> ToggleActiveStatusAsync(
        string sourceType, 
        bool isActive, 
        CancellationToken cancellationToken = default)
    {
        var rule = await _context.SourceRules
            .FirstOrDefaultAsync(r => r.SourceType == sourceType.Trim().ToUpperInvariant(), cancellationToken);

        if (rule is null)
        {
            return ServiceResult<bool>.Fail($"Source rule '{sourceType}' not found.");
        }

        rule.IsActive = isActive;
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Ok(true);
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
