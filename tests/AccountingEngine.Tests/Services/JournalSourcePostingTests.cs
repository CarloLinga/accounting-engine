using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Services;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Tests.Fixtures;
using FluentAssertions;

namespace AccountingEngine.Tests.Services;

public sealed class JournalSourcePostingTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public JournalSourcePostingTests(SqliteFixture fixture) { _fixture = fixture; }

    private static string Suffix() => Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();

    private async Task<string> SeedSalesInvoiceRuleAsync()
    {
        var tag = Suffix();
        var ar = "AR" + tag;
        var revenue = "REV" + tag;
        var tax = "TAX" + tag;
        await using var context = _fixture.CreateDbContext();
        var accountService = new AccountService(context);
        var ruleService = new SourceRuleService(context);
        var sourceType = "SALES" + tag;
        foreach (var (code, name, type) in new[] {
            (ar, "Receivable", AccountType.Asset),
            (revenue, "Sales Revenue", AccountType.Revenue),
            (tax, "Tax Payable", AccountType.Liability) })
        {
            var created = await accountService.CreateAccountAsync(new CreateAccountRequest(code, name, type));
            created.Success.Should().BeTrue(created.ErrorMessage);
        }
        var rule = await ruleService.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Sales invoice posting",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = revenue, EntryType = "Credit", AmountType = "BASE_AMOUNT", Sequence = 2 },
                new() { AccountCode = tax, EntryType = "Credit", AmountType = "TAX_AMOUNT", Sequence = 3 },
            }
        });
        rule.Success.Should().BeTrue(rule.ErrorMessage);
        return sourceType;
    }

    private static PostSourceTransactionRequest ValidRequest(string sourceType, string reference) => new()
    {
        SourceType = sourceType,
        Reference = reference,
        Amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["TOTAL_AMOUNT"] = 112m,
            ["BASE_AMOUNT"] = 100m,
            ["TAX_AMOUNT"] = 12m
        }
    };

    [Fact]
    public async Task PostJournalSourceAsync_ShouldPostBalancedEntry_WhenValid()
    {
        var sourceType = await SeedSalesInvoiceRuleAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new JournalService(context);
        var reference = "INV" + Suffix();
        var result = await service.PostJournalSourceAsync(ValidRequest(sourceType, reference));
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data!.Reference.Should().Be(reference);
        result.Data!.SourceType.Should().Be(sourceType);
        result.Data!.JournalLines.Should().HaveCount(3);
        result.Data!.JournalLines.Sum(l => l.Debit).Should().Be(112m);
        result.Data!.JournalLines.Sum(l => l.Credit).Should().Be(112m);
        result.Data!.JournalLines.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.AccountCode) && !string.IsNullOrWhiteSpace(l.AccountName));
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldMatchSourceTypeCaseInsensitively()
    {
        var sourceType = await SeedSalesInvoiceRuleAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new JournalService(context);
        var request = ValidRequest(sourceType.ToLowerInvariant(), "INV" + Suffix());
        var result = await service.PostJournalSourceAsync(request with { Amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["total_amount"] = 112m, ["base_amount"] = 100m, ["tax_amount"] = 12m } });
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data!.SourceType.Should().Be(sourceType);
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldRejectUnknownSourceType()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new JournalService(context);
        var result = await service.PostJournalSourceAsync(ValidRequest("NOPE" + Suffix(), "INV" + Suffix()));
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No Source Rule found");
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldRejectInactiveRule()
    {
        var sourceType = await SeedSalesInvoiceRuleAsync();
        await using var context = _fixture.CreateDbContext();
        (await new SourceRuleService(context).ToggleActiveStatusAsync(sourceType, isActive: false)).Success.Should().BeTrue();
        var result = await new JournalService(context).PostJournalSourceAsync(ValidRequest(sourceType, "INV" + Suffix()));
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("inactive");
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldRejectMissingAmount()
    {
        var sourceType = await SeedSalesInvoiceRuleAsync();
        await using var context = _fixture.CreateDbContext();
        var request = ValidRequest(sourceType, "INV" + Suffix());
        request.Amounts.Remove("TAX_AMOUNT");
        var result = await new JournalService(context).PostJournalSourceAsync(request);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("TAX_AMOUNT");
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldRejectDuplicateReference()
    {
        var sourceType = await SeedSalesInvoiceRuleAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new JournalService(context);
        var reference = "INV" + Suffix();
        var first = await service.PostJournalSourceAsync(ValidRequest(sourceType, reference));
        var duplicate = await service.PostJournalSourceAsync(ValidRequest(sourceType, reference));
        first.Success.Should().BeTrue(first.ErrorMessage);
        duplicate.Success.Should().BeFalse();
        duplicate.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldRejectUnbalancedRule()
    {
        var tag = Suffix();
        await using var context = _fixture.CreateDbContext();
        var accountService = new AccountService(context);
        var ruleService = new SourceRuleService(context);
        var sourceType = "UNBAL" + tag;
        foreach (var (code, name, type) in new[] { ("UA" + tag, "A", AccountType.Asset), ("UB" + tag, "B", AccountType.Revenue), ("UC" + tag, "C", AccountType.Liability) })
        {
            var created = await accountService.CreateAccountAsync(new CreateAccountRequest(code, name, type));
            created.Success.Should().BeTrue(created.ErrorMessage);
        }
        var rule = await ruleService.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Unbalanced template",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = "UA" + tag, EntryType = "Debit", AmountType = "BIG", Sequence = 1 },
                new() { AccountCode = "UB" + tag, EntryType = "Credit", AmountType = "SMALL", Sequence = 2 },
                new() { AccountCode = "UC" + tag, EntryType = "Credit", AmountType = "SMALL", Sequence = 3 },
            }
        });
        rule.Success.Should().BeTrue(rule.ErrorMessage);
        var result = await new JournalService(context).PostJournalSourceAsync(new PostSourceTransactionRequest
        {
            SourceType = sourceType,
            Reference = "INV" + Suffix(),
            Amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["BIG"] = 100m, ["SMALL"] = 10m }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unbalanced");
    }

    [Fact]
    public async Task PostJournalSourceAsync_ShouldRejectManualOnlyRule()
    {
        await using var context = _fixture.CreateDbContext();
        var sourceType = "MANUAL" + Suffix();
        var created = await new SourceRuleService(context).CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType, Description = "Manual category", IsManualEntryAllowed = true
        });
        created.Success.Should().BeTrue(created.ErrorMessage);
        var result = await new JournalService(context).PostJournalSourceAsync(ValidRequest(sourceType, "INV" + Suffix()));
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("manual entries");
    }
}