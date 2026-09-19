using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

public class SalesJournalService : ISalesJournalService
{
    private readonly IAccountingDbContext _dbContext;

    public SalesJournalService(IAccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<SalesJournalResponse>> CreateAsync(
        CreateSalesJournalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceNo))
            return ServiceResult<SalesJournalResponse>.Fail("Invoice number cannot be empty.");

        var cleanInvoiceNo = request.InvoiceNo.Trim();
        var key = cleanInvoiceNo.ToLower();

        var exists = await _dbContext.SalesJournals
            .AnyAsync(s => s.InvoiceNo.ToLower() == key, cancellationToken);

        if (exists)
            return ServiceResult<SalesJournalResponse>.Fail($"Sales invoice '{cleanInvoiceNo}' already exists.");

        var totalsError = ValidateTotals(request.InvoiceAmount, request.VatAmount, request.VATableSale, request.ZeroRatedSale, request.VatExemptSale);
        if (totalsError is not null)
            return ServiceResult<SalesJournalResponse>.Fail(totalsError);

        var entry = new SalesJournal
        {
            Id = Guid.NewGuid(),
            InvoiceNo = cleanInvoiceNo,
            InvoiceDate = request.InvoiceDate,
            Customer = request.Customer?.Trim() ?? string.Empty,
            TaxIDNo = request.TaxIDNo?.Trim() ?? string.Empty,
            InvoiceAmount = request.InvoiceAmount,
            VatAmount = request.VatAmount,
            VATableSale = request.VATableSale,
            ZeroRatedSale = request.ZeroRatedSale,
            VatExemptSale = request.VatExemptSale,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.SalesJournals.Add(entry);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            if (detail.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<SalesJournalResponse>.Fail($"Sales invoice '{cleanInvoiceNo}' already exists.");
            }

            return ServiceResult<SalesJournalResponse>.Fail($"Failed to save sales invoice '{cleanInvoiceNo}': {detail}");
        }

        return ServiceResult<SalesJournalResponse>.Ok(MapToResponse(entry));
    }


    public async Task<SalesJournalResponse?> GetByInvoiceNoAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo))
            return null;

        var key = invoiceNo.Trim().ToLower();
        var entry = await _dbContext.SalesJournals
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.InvoiceNo.ToLower() == key, cancellationToken);

        return entry is null ? null : MapToResponse(entry);
    }

    public async Task<IEnumerable<SalesJournalResponse>> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SalesJournal> query = _dbContext.SalesJournals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.InvoiceNo.ToLower().Contains(term) ||
                s.Customer.ToLower().Contains(term) ||
                s.TaxIDNo.ToLower().Contains(term));
        }

        var entries = await query
            .OrderBy(s => s.InvoiceDate)
            .ThenBy(s => s.InvoiceNo)
            .ToListAsync(cancellationToken);

        return entries.Select(MapToResponse).ToList();
    }

    public async Task<ServiceResult<SalesJournalResponse>> UpdateAsync(
        string invoiceNo,
        UpdateSalesJournalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo))
            return ServiceResult<SalesJournalResponse>.Fail("Invoice number cannot be empty.");

        var key = invoiceNo.Trim().ToLower();
        var entry = await _dbContext.SalesJournals
            .FirstOrDefaultAsync(s => s.InvoiceNo.ToLower() == key, cancellationToken);

        if (entry is null)
            return ServiceResult<SalesJournalResponse>.Fail($"Sales invoice '{invoiceNo.Trim()}' was not found.");

        var totalsError = ValidateTotals(request.InvoiceAmount, request.VatAmount, request.VATableSale, request.ZeroRatedSale, request.VatExemptSale);
        if (totalsError is not null)
            return ServiceResult<SalesJournalResponse>.Fail(totalsError);

        entry.InvoiceDate = request.InvoiceDate;
        entry.Customer = request.Customer?.Trim() ?? string.Empty;
        entry.TaxIDNo = request.TaxIDNo?.Trim() ?? string.Empty;
        entry.InvoiceAmount = request.InvoiceAmount;
        entry.VatAmount = request.VatAmount;
        entry.VATableSale = request.VATableSale;
        entry.ZeroRatedSale = request.ZeroRatedSale;
        entry.VatExemptSale = request.VatExemptSale;
        entry.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<SalesJournalResponse>.Ok(MapToResponse(entry));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoiceNo))
            return ServiceResult<bool>.Fail("Invoice number cannot be empty.");

        var key = invoiceNo.Trim().ToLower();
        var entry = await _dbContext.SalesJournals
            .FirstOrDefaultAsync(s => s.InvoiceNo.ToLower() == key, cancellationToken);

        if (entry is null)
            return ServiceResult<bool>.Fail($"Sales invoice '{invoiceNo.Trim()}' was not found.");

        _dbContext.SalesJournals.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private static SalesJournalResponse MapToResponse(SalesJournal entry) => new()
    {
        Id = entry.Id,
        InvoiceNo = entry.InvoiceNo,
        InvoiceDate = entry.InvoiceDate,
        Customer = entry.Customer,
        TaxIDNo = entry.TaxIDNo,
        InvoiceAmount = entry.InvoiceAmount,
        VatAmount = entry.VatAmount,
        VATableSale = entry.VATableSale,
        ZeroRatedSale = entry.ZeroRatedSale,
        VatExemptSale = entry.VatExemptSale,
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt
    };

    private static string? ValidateTotals(decimal invoice, decimal vat, decimal vatable, decimal zero, decimal exempt)
    {
        if (invoice < 0 || vat < 0 || vatable < 0 || zero < 0 || exempt < 0)
            return "Sales journal amounts must be non-negative.";

        // Gross invoice = net VATable sales + VAT + zero-rated + VAT-exempt sales
        if (invoice != vatable + vat + zero + exempt)
            return "Invoice amount must equal VATable sales + VAT + zero-rated sales + VAT-exempt sales.";

        return null;
    }
}

