using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Application.DTOs;

public record CreateSalesJournalRequest
{
    [Required(ErrorMessage = "Invoice number is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Invoice number must be between 1 and 50 characters.")]
    public string InvoiceNo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Invoice date is required.")]
    public DateOnly InvoiceDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [StringLength(255, ErrorMessage = "Customer must not exceed 255 characters.")]
    public string Customer { get; init; } = string.Empty;

    [StringLength(50, ErrorMessage = "Tax ID number must not exceed 50 characters.")]
    public string TaxIDNo { get; init; } = string.Empty;

    [Range(0, 9999999999999.99, ErrorMessage = "Invoice amount must be non-negative.")]
    public decimal InvoiceAmount { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "VAT amount must be non-negative.")]
    public decimal VatAmount { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "VATable sale must be non-negative.")]
    public decimal VATableSale { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "Zero-rated sale must be non-negative.")]
    public decimal ZeroRatedSale { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "VAT-exempt sale must be non-negative.")]
    public decimal VatExemptSale { get; init; }
}

public record UpdateSalesJournalRequest
{
    [Required(ErrorMessage = "Invoice date is required.")]
    public DateOnly InvoiceDate { get; init; }

    [StringLength(255, ErrorMessage = "Customer must not exceed 255 characters.")]
    public string Customer { get; init; } = string.Empty;

    [StringLength(50, ErrorMessage = "Tax ID number must not exceed 50 characters.")]
    public string TaxIDNo { get; init; } = string.Empty;

    [Range(0, 9999999999999.99, ErrorMessage = "Invoice amount must be non-negative.")]
    public decimal InvoiceAmount { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "VAT amount must be non-negative.")]
    public decimal VatAmount { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "VATable sale must be non-negative.")]
    public decimal VATableSale { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "Zero-rated sale must be non-negative.")]
    public decimal ZeroRatedSale { get; init; }

    [Range(0, 9999999999999.99, ErrorMessage = "VAT-exempt sale must be non-negative.")]
    public decimal VatExemptSale { get; init; }
}

public record SalesJournalResponse
{
    public Guid Id { get; init; }
    public string InvoiceNo { get; init; } = string.Empty;
    public DateOnly InvoiceDate { get; init; }
    public string Customer { get; init; } = string.Empty;
    public string TaxIDNo { get; init; } = string.Empty;
    public decimal InvoiceAmount { get; init; }
    public decimal VatAmount { get; init; }
    public decimal VATableSale { get; init; }
    public decimal ZeroRatedSale { get; init; }
    public decimal VatExemptSale { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
