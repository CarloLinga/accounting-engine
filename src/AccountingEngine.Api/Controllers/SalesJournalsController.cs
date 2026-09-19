using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesJournalsController : ControllerBase
{
    private readonly ISalesJournalService _salesJournalService;

    public SalesJournalsController(ISalesJournalService salesJournalService)
    {
        _salesJournalService = salesJournalService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesJournalRequest request, CancellationToken cancellationToken)
    {
        var result = await _salesJournalService.CreateAsync(request, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetByInvoiceNo), new { invoiceNo = result.Data!.InvoiceNo }, result.Data);
    }

    [HttpGet("{invoiceNo}")]
    public async Task<IActionResult> GetByInvoiceNo(string invoiceNo, CancellationToken cancellationToken)
    {
        var entry = await _salesJournalService.GetByInvoiceNoAsync(invoiceNo, cancellationToken);
        if (entry is null)
            return NotFound(new { error = $"Sales invoice '{invoiceNo}' was not found." });

        return Ok(entry);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var entries = await _salesJournalService.SearchAsync(search, cancellationToken);
        return Ok(entries);
    }

    [HttpPut("{invoiceNo}")]
    public async Task<IActionResult> Update(string invoiceNo, [FromBody] UpdateSalesJournalRequest request, CancellationToken cancellationToken)
    {
        var result = await _salesJournalService.UpdateAsync(invoiceNo, request, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpDelete("{invoiceNo}")]
    public async Task<IActionResult> Delete(string invoiceNo, CancellationToken cancellationToken)
    {
        var result = await _salesJournalService.DeleteAsync(invoiceNo, cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.ErrorMessage });

        return NoContent();
    }
}
