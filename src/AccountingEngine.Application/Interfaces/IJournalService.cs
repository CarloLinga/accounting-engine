using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface IJournalService
{
    Task<ServiceResult<TransactionResponse>> PostGeneralJournalAsync(PostGeneralJournalRequest request, CancellationToken cancellationToken = default);
    Task<TransactionResponse?> GetTransactionByReferenceAsync(string reference, CancellationToken cancellationToken = default);
}
