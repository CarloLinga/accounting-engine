using AccountingEngine.Application.DTOs;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Core.Domain.Entities;
using AccountingEngine.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Application.Services;

/// <summary>
/// Balance Sheet, Income Statement (single period) and indirect-method
/// Cash Flow, all computed from the shared <see cref="IAccountBalanceProvider"/>.
/// </summary>
public partial class FinancialStatementService : IFinancialStatementService
{
    private readonly IAccountingDbContext _dbContext;
    private readonly IAccountBalanceProvider _balances;

    public FinancialStatementService(IAccountingDbContext dbContext, IAccountBalanceProvider balances)
    {
        _dbContext = dbContext;
        _balances = balances;
    }

    private async Task<List<Account>> LoadAccountsAsync(bool includeInactive, CancellationToken ct)
    {
        var query = _dbContext.Accounts.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(a => a.IsActive);
        return await query.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Code).ToListAsync(ct);
    }

    /// <summary>
    /// Natural-side balance (Asset/Expense → Debit − Credit;
    /// Liability/Equity/Revenue → Credit − Debit). Contra accounts (allowances,
    /// accumulated depreciation, discounts, drawings) already carry a negative
    /// natural balance, so they automatically net against their section — no
    /// explicit sign flip is needed. IsContra remains informational (UI grouping).
    /// </summary>
    internal static decimal PresentationAmount(Account a, decimal debit, decimal credit)
        => AccountBalanceProvider.Normalize(a.Type, debit, credit);

    private static (decimal Revenue, decimal Expense) IncomeTotals(
        List<Account> accounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> totals)
    {
        decimal revenue = 0, expense = 0;
        foreach (var a in accounts.Where(a => a.Statement == FinancialStatement.IncomeStatement && a.IsPostable))
        {
            if (!totals.TryGetValue(a.Id, out var t)) continue;
            var amount = PresentationAmount(a, t.Debit, t.Credit);
            if (a.Type == AccountType.Revenue) revenue += amount;
            else if (a.Type == AccountType.Expense) expense += amount;
        }
        return (revenue, expense);
    }

    private static decimal Delta(
        Account a,
        Dictionary<Guid, (decimal Debit, decimal Credit)> closing,
        Dictionary<Guid, (decimal Debit, decimal Credit)> opening)
    {
        closing.TryGetValue(a.Id, out var c);
        opening.TryGetValue(a.Id, out var o);
        return PresentationAmount(a, c.Debit, c.Credit) - PresentationAmount(a, o.Debit, o.Credit);
    }

    private static decimal CashBalance(
        List<Account> accounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> totals)
    {
        decimal cash = 0;
        foreach (var a in accounts.Where(a => a.IsCashEquivalent))
        {
            if (!totals.TryGetValue(a.Id, out var t)) continue;
            cash += PresentationAmount(a, t.Debit, t.Credit);
        }
        return cash;
    }

    private static string? ParentCode(List<Account> accounts, Account a)
    {
        if (!a.ParentAccountId.HasValue) return null;
        return accounts.FirstOrDefault(p => p.Id == a.ParentAccountId.Value)?.Code;
    }

    private static bool IsDescendantOf(List<Account> accounts, Guid accountId, Guid ancestorId)
    {
        var current = accounts.FirstOrDefault(a => a.Id == accountId);
        while (current?.ParentAccountId.HasValue == true)
        {
            if (current.ParentAccountId.Value == ancestorId) return true;
            current = accounts.FirstOrDefault(a => a.Id == current.ParentAccountId.Value);
        }
        return false;
    }

    private static string SectionTitle(BalanceSheetClass cls) => cls switch
    {
        BalanceSheetClass.CurrentAsset => "Current Assets",
        BalanceSheetClass.NonCurrentAsset => "Non-Current Assets",
        BalanceSheetClass.CurrentLiability => "Current Liabilities",
        BalanceSheetClass.NonCurrentLiability => "Non-Current Liabilities",
        BalanceSheetClass.Equity => "Equity",
        _ => cls.ToString()
    };

    private static string SectionTitle(IncomeStatementClass cls) => cls switch
    {
        IncomeStatementClass.OperatingRevenue => "Operating Revenue",
        IncomeStatementClass.NonOperatingRevenue => "Non-Operating Revenue",
        IncomeStatementClass.ContraRevenue => "Less: Contra Revenue",
        IncomeStatementClass.CostOfGoodsSold => "Cost of Goods Sold",
        IncomeStatementClass.OperatingExpense => "Operating Expenses",
        IncomeStatementClass.NonOperatingExpense => "Non-Operating Expenses",
        _ => cls.ToString()
    };
}
