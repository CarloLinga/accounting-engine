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

    [Fact]
    public async Task UpdateRuleAsync_ShouldUpdateUnusedRule()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "UPDATE" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Original description",
            IsManualEntryAllowed = true
        });

        var updated = await service.UpdateRuleAsync(sourceType, new UpdateSourceRuleRequest
        {
            SourceType = sourceType + "_NEW",
            Description = "Updated description",
            IsManualEntryAllowed = true
        });

        created.Success.Should().BeTrue(created.ErrorMessage);
        updated.Success.Should().BeTrue(updated.ErrorMessage);
        updated.Data!.SourceType.Should().Be(sourceType + "_NEW");
        updated.Data.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteRuleAsync_ShouldDeleteUnusedRule()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "DELETE" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Temporary rule",
            IsManualEntryAllowed = true
        });

        var deleted = await service.DeleteRuleAsync(sourceType);

        created.Success.Should().BeTrue(created.ErrorMessage);
        deleted.Success.Should().BeTrue(deleted.ErrorMessage);
        (await service.GetAllRulesAsync()).Should().NotContain(r => r.SourceType == sourceType);
    }

    [Fact]
    public async Task UpdateRuleAsync_ShouldHandleNullRuleLines_FromFrontendEditForm()
    {
        // Regression test: the Admin edit form serialises an untouched/empty
        // grid as "ruleLines": null, which System.Text.Json binds as a null
        // List (overwriting the "= new()" initializer). This used to NRE
        // inside UpdateRuleAsync and surface as a 500 InternalServerError
        // when renaming e.g. "XXX" -> "XXX_UPDATED".
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "XXX" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Original description",
            IsManualEntryAllowed = true
        });
        created.Success.Should().BeTrue(created.ErrorMessage);

        var updated = await service.UpdateRuleAsync(sourceType, new UpdateSourceRuleRequest
        {
            SourceType = sourceType + "_UPDATED",
            Description = "Updated description",
            IsManualEntryAllowed = true,
            RuleLines = null!
        });

        updated.Success.Should().BeTrue(updated.ErrorMessage ?? "null RuleLines caused a failure");
        updated.Data!.SourceType.Should().Be(sourceType + "_UPDATED");
    }

    [Fact]
    public async Task UpdateRuleByIdAsync_ShouldRenameRule_WhenUsingStableId()
    {
        // The Admin edit form renames XXX -> XXX_UPDATED. Keying the update by
        // the stable Id (instead of the mutable SourceType) means the rename
        // can never target the wrong row or miss it.
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "XXX" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Original description",
            IsManualEntryAllowed = true
        });
        created.Success.Should().BeTrue(created.ErrorMessage);

        var updated = await service.UpdateRuleByIdAsync(created.Data!.Id, new UpdateSourceRuleRequest
        {
            SourceType = sourceType + "_UPDATED",
            Description = "Updated description",
            IsManualEntryAllowed = true
        });

        updated.Success.Should().BeTrue(updated.ErrorMessage);
        updated.Data!.Id.Should().Be(created.Data!.Id);
        updated.Data!.SourceType.Should().Be(sourceType + "_UPDATED");
    }

    [Fact]
    public async Task GetRuleByIdAsync_ShouldReturnRule()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "GETID" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Get by id",
            IsManualEntryAllowed = true
        });
        created.Success.Should().BeTrue(created.ErrorMessage);

        var fetched = await service.GetRuleByIdAsync(created.Data!.Id);

        fetched.Success.Should().BeTrue(fetched.ErrorMessage);
        fetched.Data!.SourceType.Should().Be(sourceType);
    }

    [Fact]
    public async Task GetRuleByIdAsync_ShouldFail_ForUnknownId()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);

        var fetched = await service.GetRuleByIdAsync(Guid.NewGuid());

        fetched.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRuleByIdAsync_ShouldDeleteUnusedRule()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = "DELID" + Suffix(),
            Description = "Temporary rule",
            IsManualEntryAllowed = true
        });
        created.Success.Should().BeTrue(created.ErrorMessage);

        var deleted = await service.DeleteRuleByIdAsync(created.Data!.Id);

        deleted.Success.Should().BeTrue(deleted.ErrorMessage);
        (await service.GetRuleByIdAsync(created.Data!.Id)).Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRuleAsync_ShouldRejectRuleUsedByJournalEntry()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "USED" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Used rule",
            IsManualEntryAllowed = true
        });
        context.JournalEntries.Add(new AccountingEngine.Core.Domain.Entities.JournalEntry
        {
            Reference = "REF-" + Suffix(),
            SourceType = sourceType
        });
        await context.SaveChangesAsync();

        var updated = await service.UpdateRuleAsync(sourceType, new UpdateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Should not update",
            IsManualEntryAllowed = true
        });

        created.Success.Should().BeTrue(created.ErrorMessage);
        updated.Success.Should().BeFalse();
        updated.ErrorMessage.Should().Contain("used by journal entries");
    }

    [Fact]
    public async Task GetAmountTypesAsync_ShouldAlwaysIncludeWellKnownDefaults()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);

        var amountTypes = await service.GetAmountTypesAsync();

        amountTypes.Should().Contain(new[]
        {
            "TOTAL_AMOUNT", "BASE_AMOUNT", "TAX_AMOUNT",
            "FREIGHT_AMOUNT", "DISCOUNT_AMOUNT", "NET_AMOUNT", "CUSTOM"
        });
        amountTypes.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetAmountTypesAsync_ShouldMergeCustomTypesFromRuleLines_WithDefaults()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var customType = "CUSTOM_AMOUNT_" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = "AMT" + Suffix(),
            Description = "Custom amount type rule",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = customType, Sequence = 1 },
                new() { AccountCode = revenue, EntryType = "Credit", AmountType = "TOTAL_AMOUNT", Sequence = 2 },
            }
        });
        created.Success.Should().BeTrue(created.ErrorMessage);

        var amountTypes = await service.GetAmountTypesAsync();

        amountTypes.Should().Contain(customType);
        amountTypes.Should().Contain("TOTAL_AMOUNT");
        amountTypes.Should().Contain("CUSTOM");
        amountTypes.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetAmountTypesAsync_ShouldExcludeInactiveRuleTypes_WhenIncludeInactiveIsFalse()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "INACT" + Suffix();
        var uniqueType = "INACTIVE_ONLY_" + Suffix();
        var created = await service.CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Inactive rule",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = uniqueType, Sequence = 1 },
                new() { AccountCode = revenue, EntryType = "Credit", AmountType = "TOTAL_AMOUNT", Sequence = 2 },
            }
        });
        created.Success.Should().BeTrue(created.ErrorMessage);
        (await service.ToggleActiveStatusAsync(sourceType, false)).Success.Should().BeTrue();

        var all = await service.GetAmountTypesAsync(includeInactive: true);
        var activeOnly = await service.GetAmountTypesAsync(includeInactive: false);

        all.Should().Contain(uniqueType);
        activeOnly.Should().NotContain(uniqueType);
        activeOnly.Should().Contain("TOTAL_AMOUNT");
    }

    // Regression tests for the DbUpdateConcurrencyException bug: when template
    // lines were replaced together with a sourceType rename, EF classified the
    // re-added lines as Modified instead of Added, so SaveChanges emitted
    // UPDATE ... WHERE id = <new Guid>, matched 0 rows and failed with
    // "it was modified or deleted by another request".

    [Fact]
    public async Task UpdateRuleAsync_ShouldRenameSourceType_AndReplaceTemplateLines()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "REN" + Suffix();
        var created = await service.CreateRuleAsync(SalesRule(sourceType, ar, revenue, tax));
        created.Success.Should().BeTrue(created.ErrorMessage);

        var renamed = "REN" + Suffix() + "_UPDATED";
        var updated = await service.UpdateRuleAsync(sourceType, new UpdateSourceRuleRequest
        {
            SourceType = renamed,
            Description = "Renamed sales invoice posting",
            IsManualEntryAllowed = false,
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = revenue, EntryType = "Credit", AmountType = "BASE_AMOUNT", Sequence = 2 },
            }
        });

        updated.Success.Should().BeTrue(updated.ErrorMessage);
        updated.Data!.SourceType.Should().Be(renamed);
        updated.Data!.RuleLines.Should().HaveCount(2);

        var all = await service.GetAllRulesAsync();
        all.Should().Contain(r => r.SourceType == renamed);
        all.Should().NotContain(r => r.SourceType == sourceType);
    }

    [Fact]
    public async Task UpdateRuleByIdAsync_ShouldRenameSourceType_WhenBodyCarriesNewCode()
    {
        var (ar, revenue, tax) = await SeedSalesAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new SourceRuleService(context);
        var sourceType = "RID" + Suffix();
        var created = await service.CreateRuleAsync(SalesRule(sourceType, ar, revenue, tax));
        created.Success.Should().BeTrue(created.ErrorMessage);

        var renamed = "RID" + Suffix() + "_V2";
        var updated = await service.UpdateRuleByIdAsync(created.Data!.Id, new UpdateSourceRuleRequest
        {
            SourceType = renamed,
            Description = "Renamed via stable id",
            IsManualEntryAllowed = false,
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = ar, EntryType = "Debit", AmountType = "TOTAL_AMOUNT", Sequence = 1 },
                new() { AccountCode = tax, EntryType = "Credit", AmountType = "TAX_AMOUNT", Sequence = 2 },
            }
        });

        updated.Success.Should().BeTrue(updated.ErrorMessage);
        updated.Data!.SourceType.Should().Be(renamed);
        updated.Data!.RuleLines.Should().HaveCount(2);
    }
}