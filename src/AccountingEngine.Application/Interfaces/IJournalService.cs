using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface IJournalService
{
    // POST a General Journal
    Task<ServiceResult<JournalEntryResponse>> PostJournalEntryAsync(
        PostGeneralJournalRequest request, 
        CancellationToken cancellationToken = default);
    
    // POST a Source Journal
    Task<ServiceResult<JournalEntryResponse>>  PostJournalSourceAsync(
        PostSourceTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<JournalEntryResponse>> UpdateJournalEntryAsync(
        Guid id,
        UpdateJournalEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteJournalEntryAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // GET all transactions with optional filtering
    Task<List<JournalEntryResponse>> GetJournalEntriesAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        string? sourceType = null,
        CancellationToken cancellationToken = default);

    // GET a single Transaction by Id
    Task<ServiceResult<JournalEntryResponse>> GetJournalEntryByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default);
    
    // GET a single transaction by Reference
    Task<JournalEntryResponse?> GetJournalEntryByReferenceAsync(
        string reference, 
        CancellationToken cancellationToken = default);
}
