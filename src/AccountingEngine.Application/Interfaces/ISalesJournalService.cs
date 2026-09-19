using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface ISalesJournalService
{
    Task<ServiceResult<SalesJournalResponse>> CreateAsync(
        CreateSalesJournalRequest request,
        CancellationToken cancellationToken = default);

    Task<SalesJournalResponse?> GetByInvoiceNoAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<SalesJournalResponse>> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SalesJournalResponse>> UpdateAsync(
        string invoiceNo,
        UpdateSalesJournalRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default);
}
