using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Application.DTOs;

public record CreateSourceRuleRequest
{
    private readonly string _sourceType = string.Empty;
    private readonly string _description = string.Empty;
    private readonly List<CreateSourceRuleLineRequest> _ruleLines = new();

    [Required(ErrorMessage = "Source type code is required.")]
    [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Source type must be UPPERCASE with underscores (e.g., OPENING_BALANCE, SALES_INVOICE).")]
    public string SourceType
    {
        get => _sourceType;
        init => _sourceType = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Description is required.")]
    public string Description
    {
        get => _description;
        init => _description = value?.Trim() ?? string.Empty;
    }

    public bool IsManualEntryAllowed { get; init; } = false;

    // Optional for manual rules, required for automated rules.
    // Null-guarded: explicit JSON null ("ruleLines": null) stays an empty list
    // instead of NRE-ing the service (which surfaced as a 500 on update).
    public List<CreateSourceRuleLineRequest> RuleLines
    {
        get => _ruleLines;
        init => _ruleLines = value ?? new();
    }
}

public record UpdateSourceRuleRequest
{
    private readonly string _sourceType = string.Empty;
    private readonly string _description = string.Empty;
    private readonly List<CreateSourceRuleLineRequest> _ruleLines = new();

    [Required(ErrorMessage = "Source type code is required.")]
    [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Source type must be UPPERCASE with underscores (e.g., OPENING_BALANCE, SALES_INVOICE).")]
    public string SourceType
    {
        get => _sourceType;
        init => _sourceType = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Description is required.")]
    public string Description
    {
        get => _description;
        init => _description = value?.Trim() ?? string.Empty;
    }

    public bool IsManualEntryAllowed { get; init; } = false;

    // Null-guarded for the same reason as above: the Admin edit form may
    // serialise an untouched/empty grid as "ruleLines": null.
    public List<CreateSourceRuleLineRequest> RuleLines
    {
        get => _ruleLines;
        init => _ruleLines = value ?? new();
    }
}

public record CreateSourceRuleLineRequest
{
    private readonly string _accountCode = string.Empty;
    private readonly string _entryType = string.Empty;
    private readonly string _amountType = string.Empty;

    [Required(ErrorMessage = "Account code is required.")]
    public string AccountCode
    {
        get => _accountCode;
        init => _accountCode = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Posting side is required (Debit or Credit).")]
    public string EntryType
    {
        get => _entryType;
        init => _entryType = value?.Trim() ?? string.Empty;
    } // "Debit" or "Credit"

    [Required(ErrorMessage = "Amount type identifier is required (e.g., BASE_AMOUNT, TAX_AMOUNT, TOTAL_AMOUNT).")]
    public string AmountType
    {
        get => _amountType;
        init => _amountType = value?.Trim() ?? string.Empty;
    }

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
