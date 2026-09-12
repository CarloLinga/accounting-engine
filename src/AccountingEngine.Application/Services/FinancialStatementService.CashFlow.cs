using AccountingEngine.Application.DTOs;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.Services;

public partial class FinancialStatementService
{
    public async Task<ServiceResult<CashFlowStatementResponse>> GetCashFlowStatementAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return ServiceResult<CashFlowStatementResponse>.Fail("The 'from' date must be on or before the 'to' date.");

        var toDate = to ?? DateTimeOffset.UtcNow;
        var accounts = await LoadAccountsAsync(includeInactive: true, cancellationToken);

        var periodTotals = await _balances.GetTotalsAsync(from, toDate, cancellationToken);
        var (revenue, expense) = IncomeTotals(accounts, periodTotals);
        var netIncome = revenue - expense;

        var closingTotals = await _balances.GetTotalsAsync(null, toDate, cancellationToken);
        var openingTotals = from.HasValue
            ? await _balances.GetTotalsAsync(null, from.Value.AddTicks(-1), cancellationToken)
            : new Dictionary<Guid, (decimal Debit, decimal Credit)>();

        var operating = new List<CashFlowAdjustmentResponse>();
        var investing = new List<CashFlowAdjustmentResponse>();
        var financing = new List<CashFlowAdjustmentResponse>();
        var unmapped = new List<CashFlowAdjustmentResponse>();

        // Indirect method (raw identity): every non-cash balance-sheet account's
        // signed movement is a cash-flow driver, and "NetIncome" already folds the
        // income-statement sides in. By double-entry the sections sum exactly to
        // the change in the cash pool — no add-back reconciling needed. Contra
        // accounts (accumulated depreciation, allowances, drawings) are included
        // with their natural signed delta; they net automatically.
        foreach (var account in accounts.Where(a => a.Statement == FinancialStatement.BalanceSheet && a.IsPostable))
        {
            var delta = Delta(account, closingTotals, openingTotals);
            if (delta == 0 || account.IsCashEquivalent) continue;

            var cashEffect = account.Type == AccountType.Asset ? -delta : delta;
            var adj = new CashFlowAdjustmentResponse
            {
                Label = $"{account.Code} {account.Name}",
                Amount = cashEffect,
                AccountCode = account.Code
            };

            switch (account.CashFlowActivity)
            {
                case CashFlowActivity.Operating: operating.Add(adj); break;
                case CashFlowActivity.Investing: investing.Add(adj); break;
                case CashFlowActivity.Financing: financing.Add(adj); break;
                default: unmapped.Add(adj); break;
            }
        }

        var operatingSubtotal = netIncome + operating.Sum(a => a.Amount);
        var investingSubtotal = investing.Sum(a => a.Amount);
        var financingSubtotal = financing.Sum(a => a.Amount);
        var netChange = operatingSubtotal + investingSubtotal + financingSubtotal;

        var beginningCash = CashBalance(accounts, openingTotals);
        var endingCash = CashBalance(accounts, closingTotals);

        return ServiceResult<CashFlowStatementResponse>.Ok(new CashFlowStatementResponse
        {
            From = from ?? DateTimeOffset.MinValue,
            To = toDate,
            NetIncome = netIncome,
            Operating = new CashFlowSectionResponse
            {
                Key = "operating", Title = "Operating Activities",
                Adjustments = operating, Subtotal = operatingSubtotal
            },
            Investing = new CashFlowSectionResponse
            {
                Key = "investing", Title = "Investing Activities",
                Adjustments = investing, Subtotal = investingSubtotal
            },
            Financing = new CashFlowSectionResponse
            {
                Key = "financing", Title = "Financing Activities",
                Adjustments = financing, Subtotal = financingSubtotal
            },
            NetChangeInCash = netChange,
            BeginningCash = beginningCash,
            EndingCash = endingCash,
            IsBalanced = endingCash - beginningCash == netChange,
            Unmapped = unmapped
        });
    }
}
