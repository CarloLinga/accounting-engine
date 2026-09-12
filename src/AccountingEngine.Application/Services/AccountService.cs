using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

public class AccountService : IAccountService
{
    private readonly IAccountingDbContext _dbContext;

    public AccountService(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<AccountResponse>> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return ServiceResult<AccountResponse>.Fail("Account code cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<AccountResponse>.Fail("Account name cannot be empty.");

        var cleanCode = request.Code.Trim();

        var codeExists = await _dbContext.Accounts
            .AnyAsync(a => a.Code.ToLower() == cleanCode.ToLower(), cancellationToken);

        if (codeExists)
            return ServiceResult<AccountResponse>.Fail($"Account with code '{cleanCode}' already exists.");

        var classificationError = ValidateClassification(request.Type, request.Statement,
            request.BalanceSheetClass, request.IncomeStatementClass);
        if (classificationError is not null)
            return ServiceResult<AccountResponse>.Fail(classificationError);

        // Back-compat: the 3-arg CreateAccountRequest(code, name, type) leaves
        // both classes unset. Default Statement from Type so legacy
        // callers/tests keep working without specifying classification.
        var statement = request.Statement;
        var bsClass = request.BalanceSheetClass;
        var isClass = request.IncomeStatementClass;
        if (bsClass is null && isClass is null)
        {
            if (request.Type is AccountType.Revenue or AccountType.Expense)
            {
                statement = FinancialStatement.IncomeStatement;
                isClass = request.Type == AccountType.Revenue
                    ? IncomeStatementClass.OperatingRevenue
                    : IncomeStatementClass.OperatingExpense;
            }
            else
            {
                statement = FinancialStatement.BalanceSheet;
                bsClass = request.Type switch
                {
                    AccountType.Asset => BalanceSheetClass.CurrentAsset,
                    AccountType.Liability => BalanceSheetClass.CurrentLiability,
                    AccountType.Equity => BalanceSheetClass.Equity,
                    _ => bsClass
                };
            }
        }

        Account? parent = null;
        if (!string.IsNullOrWhiteSpace(request.ParentAccountCode))
        {
            var parentCode = request.ParentAccountCode.Trim();
            parent = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Code.ToLower() == parentCode.ToLower(), cancellationToken);
            if (parent is null)
                return ServiceResult<AccountResponse>.Fail($"Parent account '{parentCode}' was not found.");
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Code = cleanCode,
            Name = request.Name.Trim(),
            Type = request.Type,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Statement = statement,
            BalanceSheetClass = bsClass,
            IncomeStatementClass = isClass,
            CashFlowActivity = request.CashFlowActivity,
            IsCashEquivalent = request.IsCashEquivalent,
            IsContra = request.IsContra,
            IsPostable = request.IsPostable,
            ParentAccountId = parent?.Id,
            DisplayOrder = request.DisplayOrder
        };

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AccountResponse>.Ok(await MapToResponseAsync(account, cancellationToken));
    }

    public async Task<AccountResponse?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Trim().ToLower();
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code.ToLower() == cleanCode, cancellationToken);

        return account is null ? null : await MapToResponseAsync(account, cancellationToken);
    }

    public async Task<IEnumerable<AccountResponse>> SearchAccountsAsync(string? search, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Accounts.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim().ToLower();
            query = query.Where(a => a.Code.ToLower().Contains(cleanSearch) || 
                                     a.Name.ToLower().Contains(cleanSearch));
        }

        var accounts = await query.OrderBy(a => a.Code).ToListAsync(cancellationToken);

        // Resolve parent codes in one query to avoid N+1.
        var parentIds = accounts.Where(a => a.ParentAccountId.HasValue).Select(a => a.ParentAccountId!.Value).Distinct().ToList();
        var parentCodes = parentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Accounts.AsNoTracking()
                .Where(a => parentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Code, cancellationToken);

        return accounts.Select(a => MapToResponse(a, a.ParentAccountId.HasValue && parentCodes.TryGetValue(a.ParentAccountId.Value, out var c) ? c : null));
    }

    public async Task<ServiceResult<AccountResponse>> UpdateAccountAsync(string code, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Trim().ToLower();
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Code.ToLower() == cleanCode, cancellationToken);

        if (account is null)
            return ServiceResult<AccountResponse>.Fail($"Account with code '{code}' was not found.");

        account.Name = request.Name.Trim();
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AccountResponse>.Ok(await MapToResponseAsync(account, cancellationToken));
    }

    public async Task<ServiceResult<bool>> ToggleActiveStatusAsync(string code, bool isActive, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Trim().ToLower();
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Code.ToLower() == cleanCode, cancellationToken);

        if (account is null)
            return ServiceResult<bool>.Fail($"Account with code '{code}' was not found.");

        account.IsActive = isActive;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> DeleteAccountAsync(string code, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Trim().ToLower();
        var account = await _dbContext.Accounts
            .Include(a => a.JournalEntryLines)
            .FirstOrDefaultAsync(a => a.Code.ToLower() == cleanCode, cancellationToken);

        if (account is null)
            return ServiceResult<bool>.Fail($"Account with code '{code}' was not found.");

        if (account.JournalEntryLines.Count > 0)
            return ServiceResult<bool>.Fail($"Cannot delete account '{account.Code}' because it has posted journal entries attached.");

        _dbContext.Accounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private static AccountResponse MapToResponse(Account account) => new(
        account.Id,
        account.Code,
        account.Name,
        account.Type,
        account.IsActive,
        account.CreatedAt,
        account.UpdatedAt,
        account.Statement,
        account.BalanceSheetClass,
        account.IncomeStatementClass,
        account.CashFlowActivity,
        account.IsCashEquivalent,
        account.IsContra,
        account.IsPostable,
        null,
        account.DisplayOrder
    );

    private async Task<AccountResponse> MapToResponseAsync(Account account, CancellationToken ct)
    {
        string? parentCode = null;
        if (account.ParentAccountId.HasValue)
        {
            parentCode = await _dbContext.Accounts.AsNoTracking()
                .Where(a => a.Id == account.ParentAccountId.Value)
                .Select(a => a.Code)
                .FirstOrDefaultAsync(ct);
        }
        return MapToResponse(account, parentCode);
    }

    private static AccountResponse MapToResponse(Account account, string? parentCode) => new(
        account.Id,
        account.Code,
        account.Name,
        account.Type,
        account.IsActive,
        account.CreatedAt,
        account.UpdatedAt,
        account.Statement,
        account.BalanceSheetClass,
        account.IncomeStatementClass,
        account.CashFlowActivity,
        account.IsCashEquivalent,
        account.IsContra,
        account.IsPostable,
        parentCode,
        account.DisplayOrder
    );

    private static string? ValidateClassification(
        AccountType type,
        FinancialStatement statement,
        BalanceSheetClass? bsClass,
        IncomeStatementClass? isClass)
    {
        // Explicit mismatches are rejected; fully-unset classification is
        // allowed here and defaulted by the caller (legacy 3-arg requests).
        if (bsClass is null && isClass is null) return null;
        var isBalanceSheetType = type is AccountType.Asset or AccountType.Liability or AccountType.Equity;
        var wantBalanceSheet = statement == FinancialStatement.BalanceSheet;
        if (isBalanceSheetType != wantBalanceSheet)
            return $"Account type '{type}' belongs on the {(isBalanceSheetType ? "Balance Sheet" : "Income Statement")}.";
        if (wantBalanceSheet && isClass is not null)
            return "Balance-sheet accounts must not set IncomeStatementClass.";
        if (!wantBalanceSheet && bsClass is not null)
            return "Income-statement accounts must not set BalanceSheetClass.";
        return null;
    }
}
