using System.ComponentModel.DataAnnotations;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.DTOs;

public record CreateAccountRequest
{
    private readonly string _code = string.Empty;
    private readonly string _name = string.Empty;

    public CreateAccountRequest() { }

    public CreateAccountRequest(string code, string name, AccountType type)
    {
        Code = code;
        Name = name;
        Type = type;
    }

    [Required(ErrorMessage = "Account code is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Code must be between 1 and 50 characters.")]
    public string Code 
    { 
        get => _code; 
        init => _code = value?.Trim() ?? string.Empty; 
    }

    [Required(ErrorMessage = "Account name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name 
    { 
        get => _name; 
        init => _name = value?.Trim() ?? string.Empty; 
    }

    [Required(ErrorMessage = "Account type is required.")]
    public AccountType Type { get; init; }

    public FinancialStatement Statement { get; init; } = FinancialStatement.IncomeStatement;

    public BalanceSheetClass? BalanceSheetClass { get; init; }

    public IncomeStatementClass? IncomeStatementClass { get; init; }

    public CashFlowActivity CashFlowActivity { get; init; } = CashFlowActivity.Unclassified;

    public bool IsCashEquivalent { get; init; } = false;

    public bool IsContra { get; init; } = false;

    public bool IsPostable { get; init; } = true;

    public string? ParentAccountCode { get; init; }

    public int DisplayOrder { get; init; } = 0;
}

public record UpdateAccountRequest
{
    private readonly string _name = string.Empty;

    [Required(ErrorMessage = "Account name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name 
    { 
        get => _name; 
        init => _name = value?.Trim() ?? string.Empty; 
    }

    public bool IsActive { get; init; }

    [Required(ErrorMessage = "Account type is required.")]
    public AccountType Type { get; init; }

    public FinancialStatement Statement { get; init; } = FinancialStatement.IncomeStatement;

    public BalanceSheetClass? BalanceSheetClass { get; init; }

    public IncomeStatementClass? IncomeStatementClass { get; init; }

    public CashFlowActivity CashFlowActivity { get; init; } = CashFlowActivity.Unclassified;

    public bool IsCashEquivalent { get; init; } = false;

    public bool IsContra { get; init; } = false;

    public bool IsPostable { get; init; } = true;

    public string? ParentAccountCode { get; init; }
}

public record AccountResponse(
    Guid Id,
    string Code,
    string Name,
    AccountType Type,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    FinancialStatement Statement,
    BalanceSheetClass? BalanceSheetClass,
    IncomeStatementClass? IncomeStatementClass,
    CashFlowActivity CashFlowActivity,
    bool IsCashEquivalent,
    bool IsContra,
    bool IsPostable,
    string? ParentAccountCode,
    int DisplayOrder
);

public record ServiceResult<T>(
    bool Success,
    T? Data,
    string? ErrorMessage
)
{
    public static ServiceResult<T> Ok(T data) => new(true, data, null);
    public static ServiceResult<T> Fail(string message) => new(false, default, message);
}
