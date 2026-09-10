using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Services;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Tests.Fixtures;
using FluentAssertions;

namespace AccountingEngine.Tests.Services;

public sealed class SourceRuleServiceTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public SourceRuleServiceTests(SqliteFixture fixture) { _fixture = fixture; }

    private static string Suffix() => Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();

    private async Task<(string Ar, string Revenue, string Tax)> SeedSalesAccountsAsync()
    {
        var tag = Suffix();
        var ar = "AR" + tag;
        var revenue = "REV" + tag;
        var tax = "TAX" + tag;
        await using var context = _fixture.CreateDbContext();
        var svc = new AccountService(context);
        foreach (var (code, name, type) in new[] {
            (ar, "Receivable", AccountType.Asset),
            (revenue, "Sales Revenue", AccountType.Revenue),
            (tax, "Tax Payable", AccountType.Liability) })
        {
            var r = await svc.CreateAccountAsync(new CreateAccountRequest(code, name, type));
            r.Success.Should().BeTrue(r.ErrorMessage);
        }
        return (ar, revenue, tax);
    }

    private static CreateSourceRuleRequest SalesRule(string sourceType, string ar, string revenue, string tax) => new()
    {
        SourceType = sourceType,
        Description = "Sales invoice posting",
        RuleLines = new List<CreateSourceRuleLineRequest>
        {
            new() { AccountCode = ar, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
            new() { AccountCode = revenue, EntryType = "Credit", AmountType = "BASE_AMOUNT", Sequence = 2 },
            new() { AccountCode = tax, EntryType = "Credit", AmountType = "TAX_AMOUNT", Sequence = 3 },
        }
    };

    [Fact]
    public async Task CreateRuleAsync_ShouldCreateManualHeaderRule()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "MANUAL" + Suffix();
        var result = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType, Description = "Manual category", IsManualEntryAllowed = true
        });
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data!.SourceType.Should().Be(sourceType);
        result.Data!.IsManualEntryAllowed.Should().BeTrue();
        result.Data!.RuleLines.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldCreateAutomatedRule_WhenValid()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "SALES" + Suffix();
        var result = await service.CreateRuleAsync(SalesRule(sourceType, ar, revenue, tax));
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data!.SourceType.Should().Be(sourceType);
        result.Data!.RuleLines.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldRejectDuplicateSourceType()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "DUP" + Suffix();
        var first = await service.CreateRuleAsync(SalesRule(sourceType, ar, revenue, tax));
        var duplicate = await service.CreateRuleAsync(SalesRule(sourceType, ar, revenue, tax));
        first.Success.Should().BeTrue(first.ErrorMessage);
        duplicate.Success.Should().BeFalse();
        duplicate.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldRejectManualRuleWithLines()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var result = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = "MANUAL" + Suffix(),
            Description = "Manual with lines",
            IsManualEntryAllowed = true,
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = "1010", EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 }
            }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("header-only");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldRejectAutomatedRule_WithoutDebitAndCredit()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var result = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = "BAD" + Suffix(),
            Description = "Debits only",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = revenue, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 2 },
            }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Debit").And.Contain("Credit");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldRejectInvalidEntryType_WithoutThrowing()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        Func<Task<ServiceResult<SourceRuleResponse>>> act = () => service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = "SIDE" + Suffix(),
            Description = "Invalid side",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Sideways", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = revenue, EntryType = "Credit", AmountType = "TOTAL_AMOUNT", Sequence = 2 },
            }
        });
        var result = await act.Should().NotThrowAsync();
        result.Subject.Success.Should().BeFalse();
        result.Subject.ErrorMessage.Should().Contain("Invalid posting side");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldRejectMissingAccounts()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var tag = Suffix();
        var result = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = "MISS" + tag,
            Description = "Missing",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = "NOPE" + tag + "A", EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = "NOPE" + tag + "B", EntryType = "Credit", AmountType = "TOTAL_AMOUNT", Sequence = 2 },
            }
        });
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NOPE" + tag + "A");
    }

    [Fact]
    public async Task ToggleActiveStatusAsync_ShouldDeactivateRule()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "TOGGLE" + Suffix();
        var created = await service.CreateRuleAsync(SalesRule(sourceType, ar, revenue, tax));
        created.Success.Should().BeTrue(created.ErrorMessage);
        var result = await service.ToggleActiveStatusAsync(sourceType, isActive: false);
        result.Success.Should().BeTrue(result.ErrorMessage);
        var rules = await service.GetAllRulesAsync();
        rules.Should().ContainSingle(r => r.SourceType == sourceType && !r.IsActive);
    }
}