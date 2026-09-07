using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accountService.CreateAccountAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetAccountByCode), new { code = result.Data!.Code }, result.Data);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetAccountByCode(string code, CancellationToken cancellationToken)
    {
        var account = await _accountService.GetByCodeAsync(code, cancellationToken);
        if (account is null)
        {
            return NotFound(new { error = $"Account with code '{code}' was not found." });
        }

        return Ok(account);
    }

    [HttpGet]
    public async Task<IActionResult> SearchAccounts([FromQuery] string? search, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var accounts = await _accountService.SearchAccountsAsync(search, includeInactive, cancellationToken);
        return Ok(accounts);
    }

    [HttpPut("{code}")]
    public async Task<IActionResult> UpdateAccount(string code, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accountService.UpdateAccountAsync(code, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    [HttpPatch("{code}/active")]
    public async Task<IActionResult> ToggleActiveStatus(string code, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        var result = await _accountService.ToggleActiveStatusAsync(code, isActive, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> DeleteAccount(string code, CancellationToken cancellationToken)
    {
        var result = await _accountService.DeleteAccountAsync(code, cancellationToken);
        if (!result.Success)
        {
            return Conflict(new { error = result.ErrorMessage });
        }

        return NoContent();
    }
}
