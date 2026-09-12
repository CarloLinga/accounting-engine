using System.ComponentModel.DataAnnotations;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Core.Domain.Entities;

namespace AccountingEngine.Core.Domain.Entities;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    // ----- Financial-statement classification (v1: headers + single-period) -----

    /// <summary>
    /// Parent header account for report grouping (null = top-level).
    /// Headers carry IsPostable=false and never receive journal lines directly.
    /// </summary>
    public Guid? ParentAccountId { get; set; }

    public Account? ParentAccount { get; set; }

    public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();

    /// <summary>
    /// When false this account is a report header and cannot be posted to.
    /// </summary>
    public bool IsPostable { get; set; } = true;

    /// <summary>Which primary statement this account rolls into.</summary>
    /// <remarks>
    /// CLR default (IncomeStatement) intentionally matches the AccountService
    /// back-compat defaulting for legacy 3-arg create requests, so SQLite
    /// EnsureCreated schemas and service-created rows agree. The migration
    /// backfills existing DB rows explicitly; see the migration SQL below.
    /// </remarks>
    public FinancialStatement Statement { get; set; } = FinancialStatement.IncomeStatement;

    /// <summary>Balance-sheet section (null for income-statement accounts).</summary>
    public BalanceSheetClass? BalanceSheetClass { get; set; }

    /// <summary>Income-statement grouping (null for balance-sheet accounts).</summary>
    public IncomeStatementClass? IncomeStatementClass { get; set; }

    /// <summary>
    /// Cash-flow section for balance-sheet movements (indirect method).
    /// Income-statement accounts are not classified; their effect flows
    /// through Net Income as the cash-flow starting point.
    /// </summary>
    public CashFlowActivity CashFlowActivity { get; set; } = CashFlowActivity.Unclassified;

    /// <summary>
    /// Marks the cash pool the cash-flow statement reconciles to
    /// (e.g. Cash on Hand, Operating/Payroll bank accounts).
    /// </summary>
    public bool IsCashEquivalent { get; set; } = false;

    /// <summary>
    /// Contra accounts (allowances, accumulated depreciation, discounts,
    /// drawings) present with flipped sign inside their section.
    /// </summary>
    public bool IsContra { get; set; } = false;

    /// <summary>Ordering within a report section, independent of code sort.</summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation Properties
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}
