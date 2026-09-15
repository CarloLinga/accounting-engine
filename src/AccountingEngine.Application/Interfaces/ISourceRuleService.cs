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

    Task<ServiceResult<bool>> DeleteRuleAsync(
        string sourceType,
        CancellationToken cancellationToken = default);

    Task<List<SourceRuleResponse>> GetAllRulesAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ToggleActiveStatusAsync(
        string sourceType, 
        bool isActive, 
        CancellationToken cancellationToken = default);
}
