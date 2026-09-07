using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Core.Domain.Entities;
public class SourceRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string SourceType { get; set; } = string.Empty; // e.g., "SALES_INVOICE", "OPENING_BALANCE"

    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When true, this rule acts as a validated manual category header (Lines collection remains empty).
    /// When false, it is an automated posting rule template (Lines collection must define Debit/Credit mappings).
    /// </summary>
    public bool IsManualEntryAllowed { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    // Child lines for automated rules (empty for header-only manual categories)
    public ICollection<SourceRuleLine> Lines { get; set; } = new List<SourceRuleLine>();
}
