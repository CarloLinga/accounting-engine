namespace AccountingEngine.Core.Domain.Entities;

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    // Explicit Debit and Credit amounts (Non-negative)
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public int Sequence { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}