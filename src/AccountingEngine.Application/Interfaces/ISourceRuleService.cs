using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Application.Interfaces;

public interface ISourceRuleService
{
    Task<ServiceResult<SourceRuleResponse>> CreateRuleAsync(
        CreateSourceRuleRequest request, 
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SourceRuleResponse>> UpdateRuleAsync(
        string sourceType,
        UpdateSourceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SourceRuleResponse>> UpdateRuleByIdAsync(
        Guid id,
        UpdateSourceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteRuleAsync(
        string sourceType,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteRuleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<SourceRuleResponse>> GetAllRulesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct AmountType identifiers referenced by source-rule template
    /// lines (e.g. TOTAL_AMOUNT, BASE_AMOUNT, TAX_AMOUNT), merged with the
    /// engine's well-known defaults so a fresh database still returns a
    /// useful suggestion list. Sorted alphabetically.
    /// </summary>
    Task<List<string>> GetAmountTypesAsync(
        bool includeInactive = true,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SourceRuleResponse>> GetRuleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ToggleActiveStatusAsync(
        string sourceType, 
        bool isActive, 
        CancellationToken cancellationToken = default);
}
