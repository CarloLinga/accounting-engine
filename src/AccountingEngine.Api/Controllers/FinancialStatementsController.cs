using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/financial-statements")]
[Produces("application/json")]
public class FinancialStatementsController : ControllerBase
{
    private readonly IFinancialStatementService _statements;

    public FinancialStatementsController(IFinancialStatementService statements)
    {
        _statements = statements;
    }

    /// <summary>Returns the Balance Sheet as of a cut-off date.</summary>
    [HttpGet("balance-sheet")]
    [ProducesResponseType(typeof(BalanceSheetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalanceSheet(
        [FromQuery] DateTimeOffset? asOf,
        [FromQuery] bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _statements.GetBalanceSheetAsync(asOf, includeInactiveAccounts, cancellationToken);
        if (!result.Success) return BadRequest(new { error = result.ErrorMessage });
        return Ok(result.Data);
    }

    /// <summary>Returns the single-period Income Statement.</summary>
    [HttpGet("income-statement")]
    [ProducesResponseType(typeof(IncomeStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncomeStatement(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _statements.GetIncomeStatementAsync(from, to, includeInactiveAccounts, cancellationToken);
        if (!result.Success) return BadRequest(new { error = result.ErrorMessage });
        return Ok(result.Data);
    }

    /// <summary>Returns the indirect-method Statement of Cash Flows.</summary>
    [HttpGet("cash-flow")]
    [ProducesResponseType(typeof(CashFlowStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var result = await _statements.GetCashFlowStatementAsync(from, to, cancellationToken);
        if (!result.Success) return BadRequest(new { error = result.ErrorMessage });
        return Ok(result.Data);
    }
}
