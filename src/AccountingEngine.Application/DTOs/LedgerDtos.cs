using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.DTOs;

/// <summary>
/// A single posted line within an account's General Ledger statement.
/// </summary>
public record LedgerLineResponse
{
    public Guid Id { get; init; }
    public Guid JournalEntryId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>Posting date of the journal entry this line belongs to.</summary>
    public DateTimeOffset PostedAt { get; init; }

    public decimal Debit { get; init; }
    public decimal Credit { get; init; }

    /// <summary>Running account balance after this line, on the account type's natural side.</summary>
    public decimal Balance { get; init; }

    public int Sequence { get; init; }
}

/// <summary>
/// General Ledger statement for one account over an optional date range,
/// ordered by posting date (then line sequence).
/// </summary>
public record LedgerStatementResponse
{
    public Guid AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Start of the report window (null = no lower bound).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Inclusive end of the report window.</summary>
    public DateTimeOffset To { get; init; }

    /// <summary>
    /// Balance contributed by postings strictly before the From date,
    /// expressed on the account type's natural side (zero when no From is set).
    /// </summary>
    public decimal BeginningBalance { get; init; }

    /// <summary>Sum of Debit amounts over the report window.</summary>
    public decimal TotalDebit { get; init; }

    /// <summary>Sum of Credit amounts over the report window.</summary>
    public decimal TotalCredit { get; init; }

    /// <summary>Account balance at the end of the report window.</summary>
    public decimal EndingBalance { get; init; }

    public List<LedgerLineResponse> Lines { get; init; } = new();
}