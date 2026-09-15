using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourceRulesController : ControllerBase
{
    private readonly ISourceRuleService _ruleService;

    public SourceRulesController(ISourceRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var rules = await _ruleService.GetAllRulesAsync(cancellationToken);
        return Ok(rules);
    }

    /// <summary>
    /// Amount-type identifiers available for source-rule template lines:
    /// the distinct values already referenced by existing rules merged with
    /// the engine's well-known defaults, sorted alphabetically. Lets admin
    /// clients drive their amount-type suggestions from the API instead of
    /// a hard-coded list. Pass includeInactive=false to only include types
    /// referenced by active rules.
    /// </summary>
    [HttpGet("amount-types")]
    public async Task<IActionResult> GetAmountTypes(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var amountTypes = await _ruleService.GetAmountTypesAsync(includeInactive, cancellationToken);
        return Ok(amountTypes);
    }

    /// <summary>
    /// Gets a single source rule by its stable Id. Prefer this over
    /// sourceType lookups: SourceType is mutable (renameable) so it is not
    /// a stable resource key.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _ruleService.GetRuleByIdAsync(id, cancellationToken);

        if (!result.Success)
        {
            return NotFound(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSourceRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _ruleService.CreateRuleAsync(request, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPatch("{sourceType}/status")]
    public async Task<IActionResult> ToggleStatus(string sourceType, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        var result = await _ruleService.ToggleActiveStatusAsync(sourceType, isActive, cancellationToken);
        
        if (!result.Success)
        {
            return NotFound(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// Updates a source rule located by its stable Id. Renaming
    /// <c>sourceType</c> in the body is supported -- the route Id (not the
    /// mutable code) identifies the row, so XXX -&gt; XXX_UPDATED can no
    /// longer 404 or hit the wrong row. The legacy PUT by sourceType below
    /// is kept for backward compatibility.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateById(Guid id, [FromBody] UpdateSourceRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _ruleService.UpdateRuleByIdAsync(id, request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Legacy update route keyed by the (mutable) sourceType. Prefer
    /// PUT {id} for renames; this overload stays so existing callers keep
    /// working. The {sourceType} route value is the CURRENT code and the
    /// body carries the new one.
    /// </summary>
    [HttpPut("{sourceType}")]
    public async Task<IActionResult> Update(string sourceType, [FromBody] UpdateSourceRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _ruleService.UpdateRuleAsync(sourceType, request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _ruleService.DeleteRuleByIdAsync(id, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpDelete("{sourceType}")]
    public async Task<IActionResult> Delete(string sourceType, CancellationToken cancellationToken)
    {
        var result = await _ruleService.DeleteRuleAsync(sourceType, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }
}