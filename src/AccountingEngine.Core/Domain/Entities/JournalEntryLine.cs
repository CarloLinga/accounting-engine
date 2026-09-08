using System.ComponentModel.DataAnnotations;

namespace AccountingEngine.Core.Domain.Entities;

public class JournalEntryLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    // Optional line memo/description
    public string? Description { get; set; }

    // Explicit Debit and Credit amounts
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public int Sequence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
