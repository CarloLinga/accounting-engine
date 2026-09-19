using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Core.Domain.Entities;

public class SalesJournal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string InvoiceNo { get; set; } = string.Empty;

    [Required]
    public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    
    /// <summary>
    /// Customer Name registered to BIR
    /// </summary>
    [MaxLength(255)]
    public string Customer { get; set; } = string.Empty;

    /// <summary>
    /// BIR Tax ID Number of the Customer
    /// </summary>
    [MaxLength(50)]
    public string TaxIDNo { get; set; } = string.Empty;

    /// <summary>
    /// Total Invoice Amount
    /// </summary>
    public decimal InvoiceAmount { get; set; }

    /// <summary>
    /// VAT Amount - Null for Zero-Rated and VAT Exempt Sale
    /// </summary>
    public decimal VatAmount { get; set; }

    /// <summary>
    /// VAT classification of Sale - VATable, Zero-Rate, or VAT Exempt
    /// </summary>
    public decimal VATableSale { get; set; }
    public decimal ZeroRatedSale { get; set; }
    public decimal VatExemptSale { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
