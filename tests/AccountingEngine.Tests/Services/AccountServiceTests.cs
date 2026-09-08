using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Services;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using AccountingEngine.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AccountingEngine.Tests.Services;

[Collection("Database Collection")]
public class AccountServiceTests
{
    private readonly PostgresFixture _fixture;

    public AccountServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAccountAsync_ShouldPreventDuplicateAccountCodes()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var service = new AccountService(context);

        var request1 = new CreateAccountRequest("1010", "Operating Cash", AccountType.Asset);
        var request2 = new CreateAccountRequest("1010", "Petty Cash", AccountType.Asset);

        // Act
        var result1 = await service.CreateAccountAsync(request1);
        var result2 = await service.CreateAccountAsync(request2);

        // Assert
        result1.Success.Should().BeTrue();
        result1.Data.Should().NotBeNull();
        result1.Data!.Code.Should().Be("1010");

        result2.Success.Should().BeFalse();
        result2.ErrorMessage.Should().Contain("already exists");

        // Verify database state
        var accountsCount = await context.Accounts.CountAsync(a => a.Code == "1010");
        accountsCount.Should().Be(1);
    }

    [Fact]
    public async Task ToggleActiveStatusAsync_ShouldUpdateIsActiveAndTimestamp()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var service = new AccountService(context);

        var accountCode = "1020";
        var createResult = await service.CreateAccountAsync(
            new CreateAccountRequest(accountCode, "Savings Account", AccountType.Asset));

        createResult.Success.Should().BeTrue();

        // Act - Deactivate
        var deactivateResult = await service.ToggleActiveStatusAsync(accountCode, isActive: false);

        // Assert
        deactivateResult.Success.Should().BeTrue();

        var deactivatedAccount = await service.GetByCodeAsync(accountCode);
        deactivatedAccount.Should().NotBeNull();
        deactivatedAccount!.IsActive.Should().BeFalse();
        deactivatedAccount.UpdatedAt.Should().NotBeNull();

        // Act - Reactivate
        var reactivateResult = await service.ToggleActiveStatusAsync(accountCode, isActive: true);

        // Assert
        reactivateResult.Success.Should().BeTrue();

        var reactivatedAccount = await service.GetByCodeAsync(accountCode);
        reactivatedAccount!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAccountAsync_ShouldBlockHardDelete_WhenJournalEntryLinesExist()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var service = new AccountService(context);

        var accountCode = "4010";

        // 1. Create an Account
        var accountResult = await service.CreateAccountAsync(
            new CreateAccountRequest(accountCode, "Sales Revenue", AccountType.Revenue));
        
        var accountId = accountResult.Data!.Id;

        // 2. Create a Transaction and assign a JournalEntryLine to this Account
        var journalEntry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            Reference = "INV-2026-TEST",
            Description = "Test Sale",
            PostedAt = DateTimeOffset.UtcNow
        };

        var line = new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = journalEntry.Id,
            AccountId = accountId,
            Sequence = 1,
            Debit = 0.00m,
            Credit = 250.00m
        };

        context.JournalEntries.Add(journalEntry);
        context.JournalEntryLines.Add(line);
        await context.SaveChangesAsync();

        // Act - Attempt to hard delete the account using Code
        var deleteResult = await service.DeleteAccountAsync(accountCode);

        // Assert
        deleteResult.Success.Should().BeFalse();
        deleteResult.ErrorMessage.Should().Contain("Cannot delete account because it has associated ledger postings");

        // Verify account still exists in the database
        var accountInDb = await context.Accounts.FirstOrDefaultAsync(a => a.Code == accountCode);
        accountInDb.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAccountAsync_ShouldAllowHardDelete_WhenNoPostingsOrRulesExist()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var service = new AccountService(context);

        var accountCode = "9999";

        var accountResult = await service.CreateAccountAsync(
            new CreateAccountRequest(accountCode, "Unused Temporary Account", AccountType.Expense));

        accountResult.Success.Should().BeTrue();

        // Act - Delete using Code
        var deleteResult = await service.DeleteAccountAsync(accountCode);

        // Assert
        deleteResult.Success.Should().BeTrue();

        var accountInDb = await context.Accounts.FirstOrDefaultAsync(a => a.Code == accountCode);
        accountInDb.Should().BeNull();
    }
}