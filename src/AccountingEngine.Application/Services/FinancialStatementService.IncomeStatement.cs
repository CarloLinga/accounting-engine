using AccountingEngine.Application.DTOs;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.Services;

public partial class FinancialStatementService
{
    public async Task<ServiceResult<IncomeStatementResponse>> GetIncomeStatementAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool includeInactiveAccounts = false,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return ServiceResult<IncomeStatementResponse>.Fail("The 'from' date must be on or before the 'to' date.");

        var toDate = to ?? DateTimeOffset.UtcNow;
        var totals = await _balances.GetTotalsAsync(from, toDate, cancellationToken);
        var accounts = await LoadAccountsAsync(includeInactiveAccounts, cancellationToken);

        var revenueSections = BuildIncomeSections(accounts, totals,
            [IncomeStatementClass.OperatingRevenue, IncomeStatementClass.NonOperatingRevenue, IncomeStatementClass.ContraRevenue]);
        var expenseSections = BuildIncomeSections(accounts, totals,
            [IncomeStatementClass.CostOfGoodsSold, IncomeStatementClass.OperatingExpense, IncomeStatementClass.NonOperatingExpense]);

        // Contra-revenue presents as a deduction from operating revenue.
        var operatingRevenue = ClassTotal(accounts, totals, IncomeStatementClass.OperatingRevenue)
                             + ClassTotal(accounts, totals, IncomeStatementClass.ContraRevenue);
        var nonOperatingRevenue = ClassTotal(accounts, totals, IncomeStatementClass.NonOperatingRevenue);
        var cogs = ClassTotal(accounts, totals, IncomeStatementClass.CostOfGoodsSold);
        var opex = ClassTotal(accounts, totals, IncomeStatementClass.OperatingExpense);
        var nonOpex = ClassTotal(accounts, totals, IncomeStatementClass.NonOperatingExpense);

        var totalRevenue = operatingRevenue + nonOperatingRevenue;
        var grossProfit = totalRevenue - cogs;
        var operatingIncome = grossProfit - opex;
        var netIncome = operatingIncome + nonOperatingRevenue - nonOpex;

        return ServiceResult<IncomeStatementResponse>.Ok(new IncomeStatementResponse
        {
            From = from ?? DateTimeOffset.MinValue,
            To = toDate,
            IncludeInactiveAccounts = includeInactiveAccounts,
            RevenueSections = revenueSections,
            ExpenseSections = expenseSections,
            TotalRevenue = totalRevenue,
            TotalCostOfGoodsSold = cogs,
            GrossProfit = grossProfit,
            TotalOperatingExpenses = opex,
            OperatingIncome = operatingIncome,
            TotalNonOperating = nonOperatingRevenue - nonOpex,
            NetIncome = netIncome
        });
    }

    private List<StatementSectionResponse> BuildIncomeSections(
        List<Account> accounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> totals,
        IncomeStatementClass[] classes)
    {
        var sections = new List<StatementSectionResponse>();
        foreach (var cls in classes)
        {
            var lines = accounts
                .Where(a => a.Statement == FinancialStatement.IncomeStatement
                         && a.IncomeStatementClass == cls
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
                .OrderBy(l => l.DisplayOrder).ThenBy(l => l.AccountCode)
                .ToList();

            sections.Add(new StatementSectionResponse
            {
                Key = cls.ToString(),
                Title = SectionTitle(cls),
                Lines = lines,
                Subtotal = ClassTotal(accounts, totals, cls)
            });
        }
        return sections;
    }

    private static decimal ClassTotal(
        List<Account> accounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> totals,
        IncomeStatementClass cls)
    {
        decimal sum = 0;
        foreach (var a in accounts.Where(a => a.Statement == FinancialStatement.IncomeStatement
                                           && a.IncomeStatementClass == cls
                                           && a.IsPostable))
        {
            totals.TryGetValue(a.Id, out var t);
            sum += PresentationAmount(a, t.Debit, t.Credit);
        }
        return sum;
    }
}
