using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JournalsController : ControllerBase
{
    private readonly IJournalService _journalService;

    public JournalsController(IJournalService journalService)
    {
        _journalService = journalService;
    }

    /// <summary>
    /// Posts a manual journal entry using a valid SourceType (e.g., OPENING_BALANCE, GENERAL_JOURNAL).
    /// </summary>
    /// <param name="request">The journal entry payload containing SourceType, reference, date, and lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created transaction header and lines.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostJournalEntry(
        [FromBody] PostGeneralJournalRequest request, 
        CancellationToken cancellationToken)
    {
        var result = await _journalService.PostGeneralJournalAsync(request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        // Returns HTTP 201 Created with a reference link to the lookup endpoint
        return CreatedAtAction(
            nameof(GetTransactionByReference), 
            new { reference = result.Data!.Reference }, 
            result.Data);
    }

    /// <summary>
    /// Retrieves a list of posted journal transactions with optional filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<JournalEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJournalEntries(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] string? sourceType,
        CancellationToken cancellationToken)
    {
        var result = await _journalService.GetJournalEntriesAsync(startDate, endDate, sourceType, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific transaction by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _journalService.GetJournalEntryByIdAsync(id, cancellationToken);
        return transaction is null ? NotFound(new { error = $"Transaction '{id}' not found." }) : Ok(transaction);   
    }
    
    /// <summary>
    /// Retrieves a posted transaction and its journal lines by unique reference code.
    /// </summary>
    /// <param name="reference">The transaction reference string (e.g., INIT-BAL-2026, GJ23-001).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transaction detail including SourceType and line items.</returns>
    [HttpGet("reference/{reference}")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionByReference(
        [FromRoute] string reference, 
        CancellationToken cancellationToken)
    {
        var transaction = await _journalService.GetJournalEntryByReferenceAsync(reference, cancellationToken);

        if (transaction is null)
        {
            return NotFound(new { error = $"No transaction found with reference '{reference}'." });
        }

        return Ok(transaction);
    }
}
