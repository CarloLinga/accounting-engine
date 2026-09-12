using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/financial-statements")]
[Produces("application/json", "text/csv")]
public class FinancialStatementsController : ControllerBase
{
    private readonly IFinancialStatementService _statements;

    public FinancialStatementsController(IFinancialStatementService statements)
    {
        _statements = statements;
    }

    /// <summary>Returns the Balance Sheet as of a cut-off date. Add ?format=csv for Excel / Google Sheets import.</summary>
    [HttpGet("balance-sheet")]
    [ProducesResponseType(typeof(BalanceSheetResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBalanceSheet(
        [FromQuery] DateTimeOffset? asOf,
        [FromQuery] string? format,
        [FromQuery] bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _statements.GetBalanceSheetAsync(asOf, includeInactiveAccounts, cancellationToken);
        if (!result.Success) return BadRequest(new { error = result.ErrorMessage });
        return IsCsv(format)
            ? Csv("balance-sheet", FinancialStatementCsvExporter.BalanceSheet(result.Data))
            : Ok(result.Data);
    }

    /// <summary>Returns the single-period Income Statement. Add ?format=csv for Excel / Google Sheets import.</summary>
    [HttpGet("income-statement")]
    [ProducesResponseType(typeof(IncomeStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncomeStatement(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? format,
        [FromQuery] bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _statements.GetIncomeStatementAsync(from, to, includeInactiveAccounts, cancellationToken);
        if (!result.Success) return BadRequest(new { error = result.ErrorMessage });
        return IsCsv(format)
            ? Csv("income-statement", FinancialStatementCsvExporter.IncomeStatement(result.Data))
            : Ok(result.Data);
    }

    /// <summary>Returns the indirect-method Statement of Cash Flows. Add ?format=csv for Excel / Google Sheets import.</summary>
    [HttpGet("cash-flow")]
    [ProducesResponseType(typeof(CashFlowStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? format,
        CancellationToken cancellationToken = default)
    {
        var result = await _statements.GetCashFlowStatementAsync(from, to, cancellationToken);
        if (!result.Success) return BadRequest(new { error = result.ErrorMessage });
        return IsCsv(format)
            ? Csv("cash-flow", FinancialStatementCsvExporter.CashFlow(result.Data))
            : Ok(result.Data);
    }

    private static bool IsCsv(string? format) =>
        string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    /// <summary>text/csv + UTF-8 BOM + attachment disposition so Excel/Sheets handle it natively.</summary>
    private IActionResult Csv(string baseName, byte[] bytes)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{baseName}.csv\"";
        return File(bytes, "text/csv; charset=utf-8");
    }
}
