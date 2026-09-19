using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AccountingEngine.Tests.Services;

public class SalesJournalServiceTests : IClassFixture<Fixtures.SqliteFixture>
{
    private readonly Fixtures.SqliteFixture _fixture;

    public SalesJournalServiceTests(Fixtures.SqliteFixture fixture)
    {
        _fixture = fixture;
    }

    private static CreateSalesJournalRequest ValidRequest(string invoiceNo) => new()
    {
        InvoiceNo = invoiceNo,
        InvoiceDate = new DateOnly(2026, 9, 18),
        Customer = "Acme Corp",
        TaxIDNo = "123-456-789",
        InvoiceAmount = 124m,
        VatAmount = 12m,
        VATableSale = 100m,
        ZeroRatedSale = 12m,
        VatExemptSale = 0m
    };

    [Fact]
    public async Task Create_ValidRequest_PersistsAndReturnsEntry()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new Application.Services.SalesJournalService(context);
        var invoiceNo = _fixture.UniqueCode("INV");

        var result = await service.CreateAsync(ValidRequest(invoiceNo));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(invoiceNo, result.Data!.InvoiceNo);
        Assert.Equal(124m, result.Data.InvoiceAmount);

        var stored = await context.SalesJournals.AsNoTracking()
            .FirstAsync(s => s.InvoiceNo == invoiceNo);
        Assert.Equal("Acme Corp", stored.Customer);
    }

    [Fact]
    public async Task Create_DuplicateInvoiceNo_ReturnsFailure()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new Application.Services.SalesJournalService(context);
        var invoiceNo = _fixture.UniqueCode("INV");

        var first = await service.CreateAsync(ValidRequest(invoiceNo));
        Assert.True(first.Success);

        var duplicate = await service.CreateAsync(ValidRequest(invoiceNo.ToLowerInvariant()));
        Assert.False(duplicate.Success);
        Assert.Contains("already exists", duplicate.ErrorMessage);
    }

    [Fact]
    public async Task Create_UnbalancedBreakdown_ReturnsFailure()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new Application.Services.SalesJournalService(context);
        var request = ValidRequest(_fixture.UniqueCode("INV")) with { InvoiceAmount = 200m };

        var result = await service.CreateAsync(request);

        Assert.False(result.Success);
        Assert.Contains("must equal VATable sales", result.ErrorMessage);
    }

    [Fact]
    public async Task Update_ExistingEntry_UpdatesFields()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new Application.Services.SalesJournalService(context);
        var invoiceNo = _fixture.UniqueCode("INV");
        var created = await service.CreateAsync(ValidRequest(invoiceNo));
        Assert.True(created.Success);

        var result = await service.UpdateAsync(invoiceNo, new UpdateSalesJournalRequest
        {
            InvoiceDate = new DateOnly(2026, 9, 19),
            Customer = "Updated Customer",
            TaxIDNo = "999-999-999",
            InvoiceAmount = 248m,
            VatAmount = 24m,
            VATableSale = 200m,
            ZeroRatedSale = 24m,
            VatExemptSale = 0m
        });

        Assert.True(result.Success);
        Assert.Equal("Updated Customer", result.Data!.Customer);
        Assert.Equal(248m, result.Data.InvoiceAmount);

        var fetched = await service.GetByInvoiceNoAsync(invoiceNo.ToLowerInvariant());
        Assert.NotNull(fetched);
        Assert.Equal("Updated Customer", fetched!.Customer);
    }

    [Fact]
    public async Task Delete_ExistingEntry_RemovesRowAndSearchFindsOthers()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new Application.Services.SalesJournalService(context);
        var first = _fixture.UniqueCode("INV");
        var second = _fixture.UniqueCode("INV");

        Assert.True((await service.CreateAsync(ValidRequest(first))).Success);
        Assert.True((await service.CreateAsync(ValidRequest(second) with { Customer = "Second Customer" })).Success);

        var deleted = await service.DeleteAsync(first);
        Assert.True(deleted.Success);
        Assert.Null(await service.GetByInvoiceNoAsync(first));

        var results = await service.SearchAsync("Second Customer");
        Assert.Single(results);
        Assert.Equal(second, results.First().InvoiceNo);
    }
}
