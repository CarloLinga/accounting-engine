using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Application.DTOs;

public record PostSourceTransactionRequest
{
    [Required(ErrorMessage = "Source type is required.")]
    [RegularExpression(@"^[A-Za-z0-9_]+$", ErrorMessage = "Source type must contain only letters, digits and underscores (e.g., SALES_INVOICE).")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Source type must be between 1 and 100 characters.")]
    public string SourceType { get; init; } = string.Empty; // e.g., "SALES_INVOICE"

    [Required(ErrorMessage = "Transaction reference is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Reference must be between 1 and 100 characters.")]
    public string Reference { get; init; } = string.Empty;   // e.g., "INV-2026-0001"

    [Required(ErrorMessage = "Posting date is required.")]
    public DateTimeOffset PostedAt { get; init; } = DateTimeOffset.UtcNow;

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; init; }

    /// <summary>
    /// Key-value pairs mapping the rule's AmountType to actual values.
    /// Example: { "TOTAL_AMOUNT": 112.00, "BASE_AMOUNT": 100.00, "TAX_AMOUNT": 12.00 }
    /// </summary>
    [Required(ErrorMessage = "Amounts are required.")]
    [MinLength(1, ErrorMessage = "At least one amount must be provided.")]
    public Dictionary<string, decimal> Amounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
