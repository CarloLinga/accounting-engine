using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Services;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Tests.Fixtures;
using FluentAssertions;

namespace AccountingEngine.Tests.Services;

public sealed class TrialBalanceServiceTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public TrialBalanceServiceTests(SqliteFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();

    private static DateTimeOffset PeriodStart(int year) => new(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Seeds a small chart of accounts plus an OPENING_BALANCE manual rule and a
    /// CREDIT_SALE automated rule, so tests can exercise both posting paths.
    /// Every code carries a unique suffix so the shared SQLite fixture stays isolated.
    /// </summary>
    private async Task<(string Cash, string Ar, string Sales, string TaxPayable, string Equity, string Expense, string OpeningSource, string CreditSaleSource)> SeedAsync()
    {
        var tag = Suffix();
        var cash = "CASH" + tag;
        var ar = "AR" + tag;
        var sales = "REV" + tag;
        var tax = "TAX" + tag;
        var equity = "EQUITY" + tag;
        var expense = "EXP" + tag;
        var openingSource = "OPENING_BALANCE_" + tag;
        var creditSaleSource = "CREDIT_SALE_" + tag;

        await using var context = _fixture.CreateDbContext();

        var accounts = new AccountService(context);
        foreach (var (code, name, type) in new[]
        {
            (cash, "Operating Cash", AccountType.Asset),
            (ar, "Accounts Receivable", AccountType.Asset),
            (sales, "Sales Revenue", AccountType.Revenue),
            (tax, "VAT Payable", AccountType.Liability),
            (equity, "Owner's Equity", AccountType.Equity),
            (expense, "General Expense", AccountType.Expense)
        })
        {
            var created = await accounts.CreateAccountAsync(new CreateAccountRequest(code, name, type));
            created.Success.Should().BeTrue(created.ErrorMessage);
        }

        var rules = new SourceRuleService(context);
        var openingRule = await rules.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = openingSource,
            Description = "Opening balances",
            IsManualEntryAllowed = true
        });
        openingRule.Success.Should().BeTrue(openingRule.ErrorMessage);

        var saleRule = await rules.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = creditSaleSource,
            Description = "Credit sale with VAT",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = sales, EntryType = "Credit", AmountType = "BASE_AMOUNT", Sequence = 2 },
                new() { AccountCode = tax, EntryType = "Credit", AmountType = "TAX_AMOUNT", Sequence = 3 }
            }
        });
        saleRule.Success.Should().BeTrue(saleRule.ErrorMessage);

        return (cash, ar, sales, tax, equity, expense, openingSource, creditSaleSource);
    }

    private static PostSourceTransactionRequest SaleRequest(string sourceType, string reference, DateTimeOffset postedAt) => new()
    {
        SourceType = sourceType,
        Reference = reference,
        PostedAt = postedAt,
        Description = "Credit sale",
        Amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["TOTAL_AMOUNT"] = 112m,
            ["BASE_AMOUNT"] = 100m,
            ["TAX_AMOUNT"] = 12m
        }
    };

    [Fact]
    public async Task GenerateTrialBalanceAsync_ShouldSplitOpeningAndPeriodAndCloseCorrectly()
    {
        var (cash, ar, sales, tax, equity, expense, openingSource, creditSaleSource) = await SeedAsync();

        var periodStart = PeriodStart(DateTimeOffset.UtcNow.Year);
        var openingAt = periodStart.AddSeconds(-60); // "one minute before the current year"
        var saleAt = periodStart.AddDays(100);
        var expenseAt = saleAt.AddHours(1);
        var asOf = expenseAt.AddHours(1);

        await using var context = _fixture.CreateDbContext();
        var journals = new JournalService(context);

        // Opening balance posted before the fiscal year start.
        var openingEntry = await journals.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = openingSource,
            Reference = "OPEN-" + Suffix(),
            PostedAt = openingAt,
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = cash, Debit = 5000m, Sequence = 1 },
                new() { AccountCode = equity, Credit = 5000m, Sequence = 2 }
            }
        });
        openingEntry.Success.Should().BeTrue(openingEntry.ErrorMessage);

        // Automated credit sale generated from the SourceRule during the period.
        var saleEntry = await journals.PostJournalSourceAsync(
            SaleRequest(creditSaleSource, "SALE-" + Suffix(), saleAt));
        saleEntry.Success.Should().BeTrue(saleEntry.ErrorMessage);

        // Manual journal entry during the period (reuses the manual source rule).
        var expenseEntry = await journals.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = openingSource,
            Reference = "EXP-" + Suffix(),
            PostedAt = expenseAt,
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = expense, Debit = 30m, Sequence = 1 },
                new() { AccountCode = cash, Credit = 30m, Sequence = 2 }
            }
        });
        expenseEntry.Success.Should().BeTrue(expenseEntry.ErrorMessage);

        // Act
        var tbResult = await new TrialBalanceService(context).GenerateTrialBalanceAsync(asOf, periodStart.Year);
        tbResult.Success.Should().BeTrue(tbResult.ErrorMessage);
        var tb = tbResult.Data!;

        // Assert - report headers
        tb.FiscalYear.Should().Be(periodStart.Year);
        tb.PeriodStart.Should().Be(periodStart);
        tb.AsOf.Should().Be(asOf);
        tb.IsBalanced.Should().BeTrue();
        tb.TotalDebits.Should().Be(5142m);  // 5000 opening + 112 sale + 30 expense
        tb.TotalCredits.Should().Be(5142m); // 5000 opening + 100 revenue + 12 VAT + 30 expense

        // Cash: opening 5,000; in the period it is only credited 30 → closing 4,970.
        var cashLine = tb.Lines.Single(l => l.AccountCode == cash);
        cashLine.OpeningDebit.Should().Be(5000m);
        cashLine.OpeningCredit.Should().Be(0m);
        cashLine.OpeningBalance.Should().Be(5000m);
        cashLine.PeriodDebit.Should().Be(0m);
        cashLine.PeriodCredit.Should().Be(30m);
        cashLine.PeriodBalance.Should().Be(-30m);
        cashLine.ClosingDebit.Should().Be(5000m);
        cashLine.ClosingCredit.Should().Be(30m);
        cashLine.ClosingBalance.Should().Be(4970m);

        // Accounts Receivable (asset): 112 debited in the period.
        tb.Lines.Single(l => l.AccountCode == ar).ClosingDebit.Should().Be(112m);
        tb.Lines.Single(l => l.AccountCode == ar).ClosingBalance.Should().Be(112m);

        // Sales Revenue (credit-normal): 100 credited in the period.
        var salesLine = tb.Lines.Single(l => l.AccountCode == sales);
        salesLine.PeriodCredit.Should().Be(100m);
        salesLine.ClosingBalance.Should().Be(100m);

        // VAT Payable (credit-normal): 12 credited in the period.
        tb.Lines.Single(l => l.AccountCode == tax).ClosingBalance.Should().Be(12m);

        // Owner's Equity (credit-normal): 5,000 credited before the period.
        var equityLine = tb.Lines.Single(l => l.AccountCode == equity);
        equityLine.OpeningCredit.Should().Be(5000m);
        equityLine.ClosingBalance.Should().Be(5000m);

        // Expense (debit-normal): 30 debited in the period.
        tb.Lines.Single(l => l.AccountCode == expense).ClosingBalance.Should().Be(30m);
    }

    [Fact]
    public async Task GenerateTrialBalanceAsync_ShouldTreatPostingExactlyAtPeriodStartAsPeriodActivity()
    {
        var (cash, _, _, _, equity, _, openingSource, _) = await SeedAsync();

        var periodStart = PeriodStart(DateTimeOffset.UtcNow.Year);

        await using var context = _fixture.CreateDbContext();
        var journals = new JournalService(context);

        var created = await journals.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = openingSource,
            Reference = "BOUND-" + Suffix(),
            PostedAt = periodStart, // exactly on the boundary, NOT strictly before it
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = cash, Debit = 100m, Sequence = 1 },
                new() { AccountCode = equity, Credit = 100m, Sequence = 2 }
            }
        });
        created.Success.Should().BeTrue(created.ErrorMessage);

        var tbResult = await new TrialBalanceService(context)
            .GenerateTrialBalanceAsync(periodStart, periodStart.Year);
        tbResult.Success.Should().BeTrue(tbResult.ErrorMessage);

        var cashLine = tbResult.Data!.Lines.Single(l => l.AccountCode == cash);
        cashLine.OpeningDebit.Should().Be(0m);   // strictly "< periodStart" excludes it
        cashLine.PeriodDebit.Should().Be(100m);  // "[periodStart, asOf]" includes it
        cashLine.ClosingDebit.Should().Be(100m);
    }

    [Fact]
    public async Task GenerateTrialBalanceAsync_ShouldFail_WhenAsOfIsBeforeFiscalYearStart()
    {
        await using var context = _fixture.CreateDbContext();

        var justBeforeYear = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(-1);
        var result = await new TrialBalanceService(context)
            .GenerateTrialBalanceAsync(justBeforeYear, fiscalYear: 2026);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("as-of");
    }

    [Fact]
    public async Task GenerateTrialBalanceAsync_ShouldExcludeInactiveAccounts_UnlessRequested()
    {
        var (_, ar, sales, _, _, _, _, _) = await SeedAsync();

        await using var context = _fixture.CreateDbContext();
        var accountService = new AccountService(context);
        var deactivated = await accountService.ToggleActiveStatusAsync(ar, isActive: false);
        deactivated.Success.Should().BeTrue(deactivated.ErrorMessage);

        // No postings at all in this scenario → every account has a zero balance,
        // which also proves the report lists zero-activity accounts.
        var service = new TrialBalanceService(context);

        var excluded = await service.GenerateTrialBalanceAsync();
        excluded.Success.Should().BeTrue(excluded.ErrorMessage);
        excluded.Data!.Lines.Should().NotContain(l => l.AccountCode == ar);
        // sales is active but has no postings → must still appear with zero balance.
        excluded.Data!.Lines.Single(l => l.AccountCode == sales).ClosingBalance.Should().Be(0m);

        var included = await service.GenerateTrialBalanceAsync(includeInactiveAccounts: true);
        included.Success.Should().BeTrue(included.ErrorMessage);
        included.Data!.Lines.Should().Contain(l => l.AccountCode == ar && l.ClosingDebit == 0m);
    }
}