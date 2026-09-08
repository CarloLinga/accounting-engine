using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Application.DTOs;

public record PostGeneralJournalRequest
{
    /// <summary>
    /// Transaction origin/category (e.g., OPENING_BALANCE, GENERAL_JOURNAL, SALES_INVOICE)
    /// </summary>
    [Required(ErrorMessage = "Source type is required.")]
    public string SourceType { get; init; } = "GENERAL_JOURNAL"; // Default for general postings
    
    [Required(ErrorMessage = "Transaction reference is required.")]
    public string Reference { get; init; } = string.Empty; // e.g., "INIT-BAL-2026"

    [Required(ErrorMessage = "Posting date is required.")]
    public DateTimeOffset PostedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? Description { get; init; }

    [Required(ErrorMessage = "Journal entry lines are required.")]
    [MinLength(2, ErrorMessage = "A journal entry must have at least two lines.")]
    public List<JournalLineRequest> Lines { get; init; } = new();
}
public record JournalLineRequest
{
    [Required(ErrorMessage = "Account code is required.")]
    public string AccountCode { get; init; } = string.Empty;

    [Range(0, 9999999999999.99, ErrorMessage = "Debit amount must be non-negative.")]
    public decimal Debit { get; init; } = 0m;

    [Range(0, 9999999999999.99, ErrorMessage = "Credit amount must be non-negative.")]
    public decimal Credit { get; init; } = 0m;

    public string? Description { get; init; }
    
    public int Sequence { get; init; }
}

public record JournalEntryResponse
{
    public Guid Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTimeOffset PostedAt { get; init; }
    public required List<JournalLineResponse> Lines { get; init; }
}

public record JournalLineResponse
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public string? Description { get; init; }
}
