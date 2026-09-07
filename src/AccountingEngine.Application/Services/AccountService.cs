using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
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

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Code = cleanCode,
            Name = request.Name.Trim(),
            Type = request.Type,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<AccountResponse>.Ok(MapToResponse(account));
    }

    public async Task<AccountResponse?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.Trim().ToLower();
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code.ToLower() == cleanCode, cancellationToken);

        return account is null ? null : MapToResponse(account);
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
        return accounts.Select(MapToResponse);
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

        return ServiceResult<AccountResponse>.Ok(MapToResponse(account));
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
        account.UpdatedAt
    );
}
