using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.DTOs;

/// <summary>
/// One account's row in a Trial Balance report.
/// Balances are normalized to the account type's natural (debit/credit) side.
/// </summary>
public record TrialBalanceLineResponse
{
    public Guid AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }

    /// <summary>Sum of debit postings posted before the beginning of the fiscal year.</summary>
    public decimal OpeningDebit { get; init; }

    /// <summary>Sum of credit postings posted before the beginning of the fiscal year.</summary>
    public decimal OpeningCredit { get; init; }

    /// <summary>Opening balance expressed on the account type's natural side.</summary>
    public decimal OpeningBalance { get; init; }

    /// <summary>Sum of debit postings within the fiscal year (inclusive of the as-of date).</summary>
    public decimal PeriodDebit { get; init; }

    /// <summary>Sum of credit postings within the fiscal year (inclusive of the as-of date).</summary>
    public decimal PeriodCredit { get; init; }

    /// <summary>In-period net movement on the account type's natural side.</summary>
    public decimal PeriodBalance { get; init; }

    /// <summary>OpeningDebit + PeriodDebit.</summary>
    public decimal ClosingDebit { get; init; }

    /// <summary>OpeningCredit + PeriodCredit.</summary>
    public decimal ClosingCredit { get; init; }

    /// <summary>Closing balance expressed on the account type's natural side.</summary>
    public decimal ClosingBalance { get; init; }
}

/// <summary>
/// Trial Balance report: opening balances, in-period activity and closing
/// balances for every account, with a double-entry balance check.
/// </summary>
public record TrialBalanceResponse
{
    /// <summary>The fiscal year the report was generated for.</summary>
    public int FiscalYear { get; init; }

    /// <summary>Start of the fiscal year (always Jan 1 00:00 UTC of FiscalYear).</summary>
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>Inclusive cut-off date applied to in-period activity.</summary>
    public DateTimeOffset AsOf { get; init; }

    public bool IncludeInactiveAccounts { get; init; }

    /// <summary>Sum of closing Debit amounts across all lines.</summary>
    public decimal TotalDebits { get; init; }

    /// <summary>Sum of closing Credit amounts across all lines.</summary>
    public decimal TotalCredits { get; init; }

    /// <summary>True when TotalDebits equals TotalCredits.</summary>
    public bool IsBalanced { get; init; }

    /// <summary>One row per account that has opening-balance or in-period activity,
    /// ordered by account code. Accounts with no transactions are omitted.</summary>
    public List<TrialBalanceLineResponse> Lines { get; init; } = new();
}