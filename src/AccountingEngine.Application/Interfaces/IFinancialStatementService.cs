using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface IFinancialStatementService
{
    Task<ServiceResult<BalanceSheetResponse>> GetBalanceSheetAsync(
        DateTimeOffset? asOf = null,
        bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IncomeStatementResponse>> GetIncomeStatementAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CashFlowStatementResponse>> GetCashFlowStatementAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
