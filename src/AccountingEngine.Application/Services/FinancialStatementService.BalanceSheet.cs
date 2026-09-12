using AccountingEngine.Application.DTOs;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.Services;

public partial class FinancialStatementService
{
    public async Task<ServiceResult<BalanceSheetResponse>> GetBalanceSheetAsync(
        DateTimeOffset? asOf = null,
        bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        var asOfDate = asOf ?? DateTimeOffset.UtcNow;
        var totals = await _balances.GetTotalsAsync(null, asOfDate, cancellationToken);
        var accounts = await LoadAccountsAsync(includeInactiveAccounts, cancellationToken);

        // No year-end close entries exist, so surface current-period
        // Revenue − Expense explicitly or the sheet never balances.
        var (revenue, expense) = IncomeTotals(accounts, totals);
        var currentEarnings = revenue - expense;

        var assetSections = BuildBalanceSheetSections(
            accounts, totals, [BalanceSheetClass.CurrentAsset, BalanceSheetClass.NonCurrentAsset]);
        var liabilitySections = BuildBalanceSheetSections(
            accounts, totals, [BalanceSheetClass.CurrentLiability, BalanceSheetClass.NonCurrentLiability]);
        var equitySections = BuildBalanceSheetSections(
            accounts, totals, [BalanceSheetClass.Equity]);

        var totalAssets = assetSections.Sum(s => s.Subtotal);
        var totalLiabilities = liabilitySections.Sum(s => s.Subtotal);
        var totalEquity = equitySections.Sum(s => s.Subtotal) + currentEarnings;

        return ServiceResult<BalanceSheetResponse>.Ok(new BalanceSheetResponse
        {
            AsOf = asOfDate,
            IncludeInactiveAccounts = includeInactiveAccounts,
            AssetSections = assetSections,
            LiabilitySections = liabilitySections,
            EquitySections = equitySections,
            CurrentEarnings = currentEarnings,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquity = totalEquity,
            IsBalanced = totalAssets == totalLiabilities + totalEquity
        });
    }

    private List<StatementSectionResponse> BuildBalanceSheetSections(
        List<Account> accounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> totals,
        BalanceSheetClass[] classes)
    {
        var sections = new List<StatementSectionResponse>();
        foreach (var cls in classes)
        {
            var lines = accounts
                .Where(a => a.Statement == FinancialStatement.BalanceSheet
                         && a.BalanceSheetClass == cls
                         && a.IsPostable)
                .Select(a =>
                {
                    totals.TryGetValue(a.Id, out var t);
                    return new StatementLineResponse
                    {
                        AccountId = a.Id,
                        AccountCode = a.Code,
                        AccountName = a.Name,
                        AccountType = a.Type,
                        IsContra = a.IsContra,
                        IsHeader = false,
                        Amount = PresentationAmount(a, t.Debit, t.Credit),
                        DisplayOrder = a.DisplayOrder,
                        ParentAccountCode = ParentCode(accounts, a)
                    };
                })
                .ToList();

            var headers = accounts
                .Where(a => a.Statement == FinancialStatement.BalanceSheet
                         && a.BalanceSheetClass == cls
                         && !a.IsPostable)
                .Select(h => new StatementLineResponse
                {
                    AccountId = h.Id,
                    AccountCode = h.Code,
                    AccountName = h.Name,
                    AccountType = h.Type,
                    IsContra = h.IsContra,
                    IsHeader = true,
                    Amount = lines.Where(l => IsDescendantOf(accounts, l.AccountId, h.Id)).Sum(l => l.Amount),
                    DisplayOrder = h.DisplayOrder,
                    ParentAccountCode = ParentCode(accounts, h)
                })
                .ToList();

            sections.Add(new StatementSectionResponse
            {
                Key = cls.ToString(),
                Title = SectionTitle(cls),
                Lines = headers.Concat(lines).OrderBy(l => l.DisplayOrder).ThenBy(l => l.AccountCode).ToList(),
                Subtotal = lines.Sum(l => l.Amount)
            });
        }
        return sections;
    }
}
