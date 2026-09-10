using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/trial-balance")]
[Produces("application/json")]
public class TrialBalanceController : ControllerBase
{
    private readonly ITrialBalanceService _trialBalanceService;

    public TrialBalanceController(ITrialBalanceService trialBalanceService)
    {
        _trialBalanceService = trialBalanceService;
    }

    /// <summary>
    /// Generates the Trial Balance report for a fiscal year.
    /// </summary>
    /// <remarks>
    /// Postings dated before the beginning of the fiscal year are classified as
    /// Opening Balances; everything from the period start through the as-of date
    /// is classified as in-period activity. Closing balances are normalized to
    /// each account type's natural side (e.g. an Asset balance is Debit − Credit).
    /// </remarks>
    /// <param name="asOf">Inclusive report cut-off date (ISO-8601). Defaults to now (UTC).</param>
    /// <param name="fiscalYear">Fiscal year to report on. Defaults to the as-of date's year.</param>
    /// <param name="includeInactiveAccounts">When true, deactivated accounts are included. Defaults to false.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Trial Balance report with opening balances, period activity and closing balances per account.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(TrialBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] DateTimeOffset? asOf,
        [FromQuery] int? fiscalYear,
        [FromQuery] bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _trialBalanceService.GenerateTrialBalanceAsync(
            asOf, fiscalYear, includeInactiveAccounts, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }
}