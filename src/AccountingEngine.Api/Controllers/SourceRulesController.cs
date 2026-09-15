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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSourceRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _ruleService.CreateRuleAsync(request, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetAll), new { id = result.Data!.Id }, result.Data);
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