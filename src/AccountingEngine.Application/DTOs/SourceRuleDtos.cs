using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Application.DTOs;

public record CreateSourceRuleRequest
{
    [Required(ErrorMessage = "Source type code is required.")]
    [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Source type must be UPPERCASE with underscores (e.g., OPENING_BALANCE, SALES_INVOICE).")]
    public string SourceType { get; init; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; init; } = string.Empty;

    public bool IsManualEntryAllowed { get; init; } = false;

    // Optional for manual rules, required for automated rules
    public List<CreateSourceRuleLineRequest> RuleLines { get; init; } = new();
}

public record CreateSourceRuleLineRequest
{
    [Required(ErrorMessage = "Account code is required.")]
    public string AccountCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Posting side is required (Debit or Credit).")]
    public string EntryType { get; init; } = string.Empty; // "Debit" or "Credit"

    [Required(ErrorMessage = "Amount type identifier is required (e.g., BASE_AMOUNT, TAX_AMOUNT, TOTAL_AMOUNT).")]
    public string AmountType { get; init; } = string.Empty;

    public int Sequence { get; init; }
}

public record SourceRuleResponse
{
    public Guid Id { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsManualEntryAllowed { get; init; }
    public List<SourceRuleLineResponse> RuleLines { get; init; } = new();
}

public record SourceRuleLineResponse
{
    public Guid Id { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string EntryType { get; init; } = string.Empty;
    public string AmountType { get; init; } = string.Empty;
    public int Sequence { get; init; }
}
