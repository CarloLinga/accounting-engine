using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Services;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Tests.Services;

public sealed class JournalModificationTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public JournalModificationTests(SqliteFixture fixture) => _fixture = fixture;

    private static string Suffix() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private async Task<(string Debit, string Credit)> SeedAccountsAsync()
    {
        var tag = Suffix();
        var debit = "D" + tag;
        var credit = "C" + tag;
        await using var context = _fixture.CreateDbContext();
        var service = new AccountService(context);
        (await service.CreateAccountAsync(new CreateAccountRequest(debit, "Debit account", AccountType.Asset))).Success.Should().BeTrue();
        (await service.CreateAccountAsync(new CreateAccountRequest(credit, "Credit account", AccountType.Revenue))).Success.Should().BeTrue();
        return (debit, credit);
    }

    private static async Task<string> SeedManualRuleAsync(AccountingEngine.Infrastructure.Persistence.AccountingDbContext context)
    {
        var sourceType = "MANUAL_" + Suffix();
        var result = await new SourceRuleService(context).CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Manual posting",
            IsManualEntryAllowed = true
        });
        result.Success.Should().BeTrue(result.ErrorMessage);
        return sourceType;
    }

    [Fact]
    public async Task UpdateJournalEntryAsync_ShouldReplaceLinesAndHeader()
    {
        var (debit, credit) = await SeedAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new JournalService(context);
        var sourceType = await SeedManualRuleAsync(context);
        var posted = await service.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = sourceType,
            Reference = "GJ-" + Suffix(),
            Description = "Original",
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = debit, Debit = 100m, Sequence = 1 },
                new() { AccountCode = credit, Credit = 100m, Sequence = 2 }
            }
        });
        posted.Success.Should().BeTrue(posted.ErrorMessage);

        var updated = await service.UpdateJournalEntryAsync(posted.Data!.Id, new UpdateJournalEntryRequest
        {
            Reference = "GJ-UPDATED-" + Suffix(),
            Description = "Updated",
            PostedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = debit, Debit = 125m, Sequence = 1 },
                new() { AccountCode = credit, Credit = 125m, Sequence = 2 }
            }
        });

        updated.Success.Should().BeTrue(updated.ErrorMessage);
        updated.Data!.Description.Should().Be("Updated");
        updated.Data.JournalLines.Should().HaveCount(2);
        updated.Data.JournalLines.Sum(l => l.Debit).Should().Be(125m);
        updated.Data.JournalLines.Sum(l => l.Credit).Should().Be(125m);
    }

    [Fact]
    public async Task UpdateJournalEntryAsync_ShouldUpdateEntryCreatedBySource()
    {
        var (debit, credit) = await SeedAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var sourceType = "AUTO_" + Suffix();
        var rule = await new SourceRuleService(context).CreateRuleAsync(new CreateSourceRuleRequest
        {
            SourceType = sourceType,
            Description = "Automatic posting",
            RuleLines = new List<CreateSourceRuleLineRequest>
            {
                new() { AccountCode = debit, EntryType = "Debit", AmountType = "TOTAL", Sequence = 1 },
                new() { AccountCode = credit, EntryType = "Credit", AmountType = "TOTAL", Sequence = 2 }
            }
        });
        rule.Success.Should().BeTrue(rule.ErrorMessage);

        var service = new JournalService(context);
        var posted = await service.PostJournalSourceAsync(new PostSourceTransactionRequest
        {
            SourceType = sourceType,
            Reference = "AUTO-" + Suffix(),
            Amounts = new Dictionary<string, decimal> { ["TOTAL"] = 50m }
        });

        var updated = await service.UpdateJournalEntryAsync(posted.Data!.Id, new UpdateJournalEntryRequest
        {
            Reference = posted.Data.Reference,
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = debit, Debit = 75m, Sequence = 1 },
                new() { AccountCode = credit, Credit = 75m, Sequence = 2 }
            }
        });

        updated.Success.Should().BeTrue(updated.ErrorMessage);
        updated.Data!.SourceType.Should().Be(sourceType);
        updated.Data.JournalLines.Sum(l => l.Debit).Should().Be(75m);
    }

    [Fact]
    public async Task DeleteJournalEntryAsync_ShouldCascadeDeleteLines()
    {
        var (debit, credit) = await SeedAccountsAsync();
        await using var context = _fixture.CreateDbContext();
        var service = new JournalService(context);
        var sourceType = await SeedManualRuleAsync(context);
        var posted = await service.PostJournalEntryAsync(new PostGeneralJournalRequest
        {
            SourceType = sourceType,
            Reference = "DELETE-" + Suffix(),
            Lines = new List<JournalLineRequest>
            {
                new() { AccountCode = debit, Debit = 10m, Sequence = 1 },
                new() { AccountCode = credit, Credit = 10m, Sequence = 2 }
            }
        });
        posted.Success.Should().BeTrue(posted.ErrorMessage);

        var deleted = await service.DeleteJournalEntryAsync(posted.Data!.Id);

        deleted.Success.Should().BeTrue(deleted.ErrorMessage);
        (await context.JournalEntries.FindAsync(posted.Data.Id)).Should().BeNull();
        (await context.JournalEntryLines.CountAsync(l => l.JournalEntryId == posted.Data.Id)).Should().Be(0);
    }
}
