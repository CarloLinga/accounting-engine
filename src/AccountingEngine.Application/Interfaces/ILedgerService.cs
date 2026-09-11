using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface ILedgerService
{
    /// <summary>
    /// Returns the General Ledger statement for a single account code.
    /// </summary>
    /// <remarks>
    /// Lines are ordered by posting date then sequence, with a running balance
    /// expressed on the account type's natural side. Postings strictly before the
    /// optional 'from' date are rolled into the beginning balance.
    /// </remarks>
    /// <param name="accountCode">The account code (case-insensitive).</param>
    /// <param name="from">Optional window start; postings strictly before it form the beginning balance.</param>
    /// <param name="to">Optional window end (inclusive); defaults to now (UTC).</param>
    Task<ServiceResult<LedgerStatementResponse>> GetAccountStatementAsync(
        string accountCode,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}