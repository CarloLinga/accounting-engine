using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Application.Services;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Tests.Fixtures;
using FluentAssertions;

namespace AccountingEngine.Tests.Services;

public sealed class FinancialStatementServiceTests
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static CreateAccountRequest Bs(string code, string name, AccountType type, BalanceSheetClass cls,
        CashFlowActivity activity = CashFlowActivity.Unclassified, bool cash = false, bool contra = false,
        bool postable = true) => new()
        {
            Code = code, Name = name, Type = type, Statement = FinancialStatement.BalanceSheet,
            BalanceSheetClass = cls, CashFlowActivity = activity, IsCashEquivalent = cash,
            IsContra = contra, IsPostable = postable
        };

    private static CreateAccountRequest Rev(string code, string name, IncomeStatementClass cls, bool contra = false) => new()
    {
        Code = code, Name = name, Type = AccountType.Revenue, Statement = FinancialStatement.IncomeStatement,
        IncomeStatementClass = cls, IsContra = contra
    };

    private static CreateAccountRequest Exp(string code, string name, IncomeStatementClass cls) => new()
    {
        Code = code, Name = name, Type = AccountType.Expense, Statement = FinancialStatement.IncomeStatement,
        IncomeStatementClass = cls
    };

    private static async Task SeedOpeningAsync(JournalService journals, string source, string reference,
        DateTimeOffset postedAt, params (string Code, decimal Debit, decimal Credit)[] lines)
    {
        var result = await journals.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = source,
            Reference = reference,
            PostedAt = postedAt,
            Lines = lines.Select((l, i) => new JournalLineRequest
            {
                AccountCode = l.Code, Debit = l.Debit, Credit = l.Credit, Sequence = i + 1
            }).ToList()
        });
        result.Success.Should().BeTrue(result.ErrorMessage);
    }

    [Fact]
    public async Task BalanceSheet_ShouldBalance_WithCurrentEarnings()
    {
        var tag = Tag();
        var cash = "CASH" + tag; var ar = "AR" + tag; var sales = "REV" + tag;
        var equity = "EQ" + tag; var expense = "EXP" + tag;
        var source = "SRC" + tag;
        using var fixture = new SqliteFixture();
        await using var context = fixture.CreateDbContext();
        var accounts = new AccountService(context);
        foreach (var req in new[]
        {
            Bs(cash, "Cash", AccountType.Asset, BalanceSheetClass.CurrentAsset, cash: true),
            Bs(ar, "Receivables", AccountType.Asset, BalanceSheetClass.CurrentAsset, CashFlowActivity.Operating),
            Bs(equity, "Capital", AccountType.Equity, BalanceSheetClass.Equity, CashFlowActivity.Financing),
            Rev(sales, "Sales", IncomeStatementClass.OperatingRevenue),
            Exp(expense, "Rent", IncomeStatementClass.OperatingExpense)
        })
        {
            var created = await accounts.CreateAccountAsync(req);
            created.Success.Should().BeTrue(created.ErrorMessage);
        }
        var rules = new SourceRuleService(context);
        (await rules.CreateRuleAsync(new CreateSourceRuleRequest
            { SourceType = source, Description = "t", IsManualEntryAllowed = true })).Success.Should().BeTrue();
        var journals = new JournalService(context);
        var now = DateTimeOffset.UtcNow;
        await SeedOpeningAsync(journals, source, "OP-" + tag, now.AddDays(-10),
            (equity, 0, 1000), (cash, 1000, 0));
        await SeedOpeningAsync(journals, source, "SALE-" + tag, now.AddDays(-1),
            (ar, 300, 0), (sales, 0, 300));
        await SeedOpeningAsync(journals, source, "EXP-" + tag, now.AddDays(-1),
            (expense, 80, 0), (cash, 0, 80));

        var svc = new FinancialStatementService(context, new AccountBalanceProvider(context));
        var bs = await svc.GetBalanceSheetAsync(now, false);

        bs.Success.Should().BeTrue(bs.ErrorMessage);
        bs.Data!.CurrentEarnings.Should().Be(220);
        bs.Data.TotalAssets.Should().Be(1220);
        bs.Data.TotalEquity.Should().Be(1220);
        bs.Data.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task IncomeStatement_ShouldComputeProfitLadder()
    {
        var tag = Tag();
        var cash = "CASH" + tag; var sales = "REV" + tag; var disc = "DISC" + tag;
        var cogs = "COGS" + tag; var rent = "RENT" + tag; var bank = "BANK" + tag;
        var source = "SRC" + tag;
        using var fixture = new SqliteFixture();
        await using var context = fixture.CreateDbContext();
        var accounts = new AccountService(context);
        foreach (var req in new[]
        {
            Bs(cash, "Cash", AccountType.Asset, BalanceSheetClass.CurrentAsset, cash: true),
            Rev(sales, "Sales", IncomeStatementClass.OperatingRevenue),
            Rev(disc, "Discounts", IncomeStatementClass.ContraRevenue, contra: true),
            Exp(cogs, "COGS", IncomeStatementClass.CostOfGoodsSold),
            Exp(rent, "Rent", IncomeStatementClass.OperatingExpense),
            Exp(bank, "Bank fees", IncomeStatementClass.NonOperatingExpense)
        })
            (await accounts.CreateAccountAsync(req)).Success.Should().BeTrue();
        var rules = new SourceRuleService(context);
        (await rules.CreateRuleAsync(new CreateSourceRuleRequest
            { SourceType = source, Description = "t", IsManualEntryAllowed = true })).Success.Should().BeTrue();
        var journals = new JournalService(context);
        var from = DateTimeOffset.UtcNow.AddDays(-5);
        await SeedOpeningAsync(journals, source, "T-" + tag, from.AddDays(1),
            (cash, 500, 0), (sales, 0, 500));
        await SeedOpeningAsync(journals, source, "D-" + tag, from.AddDays(1),
            (disc, 20, 0), (cash, 0, 20));
        await SeedOpeningAsync(journals, source, "C-" + tag, from.AddDays(2),
            (cogs, 200, 0), (cash, 0, 200));
        await SeedOpeningAsync(journals, source, "R-" + tag, from.AddDays(2),
            (rent, 100, 0), (cash, 0, 100));
        await SeedOpeningAsync(journals, source, "B-" + tag, from.AddDays(3),
            (bank, 10, 0), (cash, 0, 10));

        var svc = new FinancialStatementService(context, new AccountBalanceProvider(context));
        var income = await svc.GetIncomeStatementAsync(from, null, false);

        income.Success.Should().BeTrue(income.ErrorMessage);
        income.Data!.TotalRevenue.Should().Be(480);
        income.Data.GrossProfit.Should().Be(280);
        income.Data.OperatingIncome.Should().Be(180);
        income.Data.NetIncome.Should().Be(170);
    }

    [Fact]
    public async Task CashFlow_ShouldReconcileCashMovement()
    {
        var tag = Tag();
        var cash = "CASH" + tag; var ar = "AR" + tag; var sales = "REV" + tag;
        var equity = "EQ" + tag;
        var source = "SRC" + tag;
        using var fixture = new SqliteFixture();
        await using var context = fixture.CreateDbContext();
        var accounts = new AccountService(context);
        foreach (var req in new[]
        {
            Bs(cash, "Cash", AccountType.Asset, BalanceSheetClass.CurrentAsset, cash: true),
            Bs(ar, "Receivables", AccountType.Asset, BalanceSheetClass.CurrentAsset, CashFlowActivity.Operating),
            Bs(equity, "Capital", AccountType.Equity, BalanceSheetClass.Equity, CashFlowActivity.Financing),
            Rev(sales, "Sales", IncomeStatementClass.OperatingRevenue)
        })
            (await accounts.CreateAccountAsync(req)).Success.Should().BeTrue();
        var rules = new SourceRuleService(context);
        (await rules.CreateRuleAsync(new CreateSourceRuleRequest
            { SourceType = source, Description = "t", IsManualEntryAllowed = true })).Success.Should().BeTrue();
        var journals = new JournalService(context);
        var from = DateTimeOffset.UtcNow.AddDays(-5);
        await SeedOpeningAsync(journals, source, "C0-" + tag, from.AddDays(-2),
            (cash, 1000, 0), (equity, 0, 1000));
        await SeedOpeningAsync(journals, source, "S1-" + tag, from.AddDays(1),
            (ar, 400, 0), (sales, 0, 400));
        await SeedOpeningAsync(journals, source, "C1-" + tag, from.AddDays(2),
            (cash, 150, 0), (ar, 0, 150));

        var svc = new FinancialStatementService(context, new AccountBalanceProvider(context));
        var cf = await svc.GetCashFlowStatementAsync(from, null);

        cf.Success.Should().BeTrue(cf.ErrorMessage);
        cf.Data!.NetIncome.Should().Be(400);
        cf.Data.BeginningCash.Should().Be(1000);
        cf.Data.EndingCash.Should().Be(1150);
        cf.Data.NetChangeInCash.Should().Be(150);
        cf.Data.IsBalanced.Should().BeTrue();
        cf.Data.Unmapped.Should().BeEmpty();
    }

    [Fact]
    public async Task PostJournalEntry_ShouldRejectHeaderAccounts()
    {
        var tag = Tag();
        var header = "HDR" + tag; var cash = "CASH" + tag;
        var source = "SRC" + tag;
        using var fixture = new SqliteFixture();
        await using var context = fixture.CreateDbContext();
        var accounts = new AccountService(context);
        (await accounts.CreateAccountAsync(
            Bs(header, "Header", AccountType.Asset, BalanceSheetClass.CurrentAsset, postable: false)))
            .Success.Should().BeTrue();
        (await accounts.CreateAccountAsync(
            Bs(cash, "Cash", AccountType.Asset, BalanceSheetClass.CurrentAsset, cash: true)))
            .Success.Should().BeTrue();
        var rules = new SourceRuleService(context);
        (await rules.CreateRuleAsync(new CreateSourceRuleRequest
            { SourceType = source, Description = "t", IsManualEntryAllowed = true })).Success.Should().BeTrue();

        var journals = new JournalService(context);
        var result = await journals.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = source,
            Reference = "H-" + tag,
            PostedAt = DateTimeOffset.UtcNow,
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = header, Debit = 10, Credit = 0, Sequence = 1 },
                new() { AccountCode = cash, Debit = 0, Credit = 10, Sequence = 2 }
            }
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("header");
    }

    [Fact]
    public async Task CashFlow_ShouldStayBalanced_WithDepreciationEntry()
    {
        var tag = Tag();
        var cash = "CASH" + tag; var equip = "EQP" + tag; var accDep = "ADEP" + tag;
        var sales = "REV" + tag; var depExp = "DEPX" + tag; var equity = "EQT" + tag;
        var source = "SRC" + tag;
        using var fixture = new SqliteFixture();
        await using var context = fixture.CreateDbContext();
        var accounts = new AccountService(context);
        foreach (var req in new[]
        {
            Bs(cash, "Cash", AccountType.Asset, BalanceSheetClass.CurrentAsset, cash: true),
            Bs(equip, "Equipment", AccountType.Asset, BalanceSheetClass.NonCurrentAsset, CashFlowActivity.Investing),
            Bs(accDep, "Accumulated Depreciation", AccountType.Asset, BalanceSheetClass.NonCurrentAsset,
                CashFlowActivity.Investing, contra: true),
            Bs(equity, "Capital", AccountType.Equity, BalanceSheetClass.Equity, CashFlowActivity.Financing),
            Rev(sales, "Sales", IncomeStatementClass.OperatingRevenue),
            Exp(depExp, "Depreciation Expense", IncomeStatementClass.OperatingExpense)
        })
            (await accounts.CreateAccountAsync(req)).Success.Should().BeTrue();
        var rules = new SourceRuleService(context);
        (await rules.CreateRuleAsync(new CreateSourceRuleRequest
            { SourceType = source, Description = "t", IsManualEntryAllowed = true })).Success.Should().BeTrue();
        var journals = new JournalService(context);
        var from = DateTimeOffset.UtcNow.AddDays(-5);
        await SeedOpeningAsync(journals, source, "C0-" + tag, from.AddDays(-2),
            (cash, 1000, 0), (equity, 0, 1000));
        // Depreciation: Dr Depreciation Expense / Cr Accumulated Depreciation (no cash).
        await SeedOpeningAsync(journals, source, "DEP-" + tag, from.AddDays(1),
            (depExp, 100, 0), (accDep, 0, 100));

        var svc = new FinancialStatementService(context, new AccountBalanceProvider(context));
        var cf = await svc.GetCashFlowStatementAsync(from, null);

        cf.Success.Should().BeTrue(cf.ErrorMessage);
        cf.Data!.NetIncome.Should().Be(-100);
        cf.Data.BeginningCash.Should().Be(1000);
        cf.Data.EndingCash.Should().Be(1000);
        cf.Data.NetChangeInCash.Should().Be(0);
        cf.Data.IsBalanced.Should().BeTrue();
        // The contra-asset movement nets the expense inside the sections.
        cf.Data.Investing.Subtotal.Should().Be(100);
    }
}

