using System.ComponentModel.DataAnnotations;
using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Core.Domain.Entities;

public class SourceRuleLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceRuleId { get; set; }
    public SourceRule SourceRule { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    /// <summary>
    /// Indicates whether this line debits or credits the target account (Debit / Credit).
    /// </summary>
    public PostingType EntryType { get; set; }

    /// <summary>
    /// Identifies which calculated field from the payload populates this line 
    /// (e.g., "BASE_AMOUNT", "TAX_AMOUNT", "TOTAL_AMOUNT").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string AmountType { get; set; } = string.Empty;

    /// <summary>
    /// Controls the execution and display order of the journal entry line.
    /// </summary>
    public int Sequence { get; set; }
}
