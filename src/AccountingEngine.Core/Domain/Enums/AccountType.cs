namespace AccountingEngine.Core.Domain.Enums;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
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
