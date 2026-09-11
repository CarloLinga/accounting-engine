using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/ledger")]
[Produces("application/json")]
public class LedgerController : ControllerBase
{
    private readonly ILedgerService _ledgerService;

    public LedgerController(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    /// <summary>
    /// Returns the General Ledger statement for an account, ordered by posting
    /// date with a running balance.
    /// </summary>
    /// <remarks>
    /// Lines are ordered by posting date then line sequence. Postings strictly
    /// before the optional 'from' date are rolled into the beginning balance, and
    /// each line's running balance is expressed on the account type's natural side
    /// (Asset/Expense → Debit − Credit; Liability/Equity/Revenue → Credit − Debit).
    /// </remarks>
    /// <param name="accountCode">The account code (case-insensitive).</param>
    /// <param name="from">Optional window start (ISO-8601).</param>
    /// <param name="to">Optional inclusive window end (ISO-8601); defaults to now (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account statement with ordered lines and totals.</returns>
    [HttpGet("{accountCode}")]
    [ProducesResponseType(typeof(LedgerStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAccountStatement(
        string accountCode,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var result = await _ledgerService.GetAccountStatementAsync(accountCode, from, to, cancellationToken);

        if (!result.Success)
        {
            // Missing accounts surface as 404; validation issues as 400.
            var notFound = result.ErrorMessage?.Contains("was not found", StringComparison.OrdinalIgnoreCase) == true;
            return notFound
                ? NotFound(new { error = result.ErrorMessage })
                : BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }
}