using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface IAccountService
{
    Task<ServiceResult<AccountResponse>> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountResponse?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<AccountResponse>> SearchAccountsAsync(string? search, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountResponse>> UpdateAccountAsync(string code, UpdateAccountRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> ToggleActiveStatusAsync(string code, bool isActive, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> DeleteAccountAsync(string code, CancellationToken cancellationToken = default);
}
