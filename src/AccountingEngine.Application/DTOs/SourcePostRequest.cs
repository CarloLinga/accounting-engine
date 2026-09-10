namespace AccountingEngine.Application.DTOs;

public record PostSourceTransactionRequest
{
    public string SourceType { get; init; } = string.Empty; // e.g., "CREDIT_INVOICE"
    public string Reference { get; init; } = string.Empty;   // e.g., "INV-2026-0001"
    public DateTimeOffset PostedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Description { get; init; }

    /// <summary>
    /// Key-value pairs mapping the rule's AmountType to actual values.
    /// Example: { "INVOICE_AMOUNT": 112.00, "SALES_AMOUNT": 100.00, "TAX_AMOUNT": 12.00 }
    /// </summary>
    public Dictionary<string, decimal> Amounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
