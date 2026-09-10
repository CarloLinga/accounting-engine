using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface ITrialBalanceService
{
    /// <summary>
    /// Generates a Trial Balance report.
    /// </summary>
    /// <remarks>
    /// Postings dated strictly before the fiscal year start (Jan 1 00:00 UTC) are
    /// classified as Opening Balances; everything from the period start through
    /// the as-of date is classified as in-period activity.
    /// </remarks>
    /// <param name="asOf">Inclusive report cut-off date. Defaults to now (UTC).</param>
    /// <param name="fiscalYear">Fiscal year to report on. Defaults to the as-of date's year.</param>
    /// <param name="includeInactiveAccounts">When true, deactivated accounts are included (defaults to false).</param>
    Task<ServiceResult<TrialBalanceResponse>> GenerateTrialBalanceAsync(
        DateTimeOffset? asOf = null,
        int? fiscalYear = null,
        bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default);
}