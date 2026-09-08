using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface IJournalService
{
    // POST  a Transaction Journal
    Task<ServiceResult<JournalEntryResponse>> PostGeneralJournalAsync(
        PostGeneralJournalRequest request, 
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
