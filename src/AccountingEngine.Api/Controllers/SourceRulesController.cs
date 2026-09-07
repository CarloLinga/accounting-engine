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
}