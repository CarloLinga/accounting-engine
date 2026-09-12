using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

/// <summary>
/// Shared per-account (debit, credit) aggregation over an arbitrary posting window.
/// </summary>
public interface IAccountBalanceProvider
{
    Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> GetTotalsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
