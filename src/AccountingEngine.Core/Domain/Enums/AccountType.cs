namespace AccountingEngine.Core.Domain.Enums;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

/// <summary>Which primary financial statement an account rolls into.</summary>
public enum FinancialStatement
{
    BalanceSheet = 1,
    IncomeStatement = 2
}

/// <summary>Balance-sheet section grouping.</summary>
public enum BalanceSheetClass
{
    CurrentAsset = 1,
    NonCurrentAsset = 2,
    CurrentLiability = 3,
    NonCurrentLiability = 4,
    Equity = 5
}

/// <summary>Income-statement grouping for a presentable single-period report.</summary>
public enum IncomeStatementClass
{
    OperatingRevenue = 1,
    NonOperatingRevenue = 2,
    ContraRevenue = 3,
    CostOfGoodsSold = 4,
    OperatingExpense = 5,
    NonOperatingExpense = 6
}

/// <summary>Cash-flow section for balance-sheet movements (indirect method).</summary>
public enum CashFlowActivity
{
    Unclassified = 0,
    Operating = 1,
    Investing = 2,
    Financing = 3
}

public enum PostingType
{
    Debit = 1,
    Credit = 2
}

public static class AccountTypeExtensions
{
    /// <summary>
    /// Returns the natural (normal) posting side for a given account type.
    /// </summary>
    public static PostingType GetNormalBalance(this AccountType accountType) => accountType switch
    {
        AccountType.Asset => PostingType.Debit,
        AccountType.Expense => PostingType.Debit,
        AccountType.Liability => PostingType.Credit,
        AccountType.Equity => PostingType.Credit,
        AccountType.Revenue => PostingType.Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(accountType), $"Unsupported AccountType: {accountType}")
    };
}
