using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Core.Domain.Entities;

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SourceType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation Properties
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}
