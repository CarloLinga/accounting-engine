using AccountingEngine.Core.Domain.Enums;

namespace AccountingEngine.Application.DTOs;

// ---------------------------------------------------------------------------
// Shared statement primitives
// ---------------------------------------------------------------------------

/// <summary>One account row inside a financial-statement section.</summary>
public record StatementLineResponse
{
    public Guid AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public bool IsContra { get; init; }
    public bool IsHeader { get; init; }

    /// <summary>Presentation amount (contra-aware sign).</summary>
    public decimal Amount { get; init; }

    public int DisplayOrder { get; init; }
    public string? ParentAccountCode { get; init; }
}

/// <summary>A grouped section (e.g. Current Assets) with its subtotal.</summary>
public record StatementSectionResponse
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<StatementLineResponse> Lines { get; init; } = new();
    public decimal Subtotal { get; init; }
}

// ---------------------------------------------------------------------------
// Balance Sheet
// ---------------------------------------------------------------------------

public record BalanceSheetResponse
{
    public DateTimeOffset AsOf { get; init; }
    public bool IncludeInactiveAccounts { get; init; }
    public List<StatementSectionResponse> AssetSections { get; init; } = new();
    public List<StatementSectionResponse> LiabilitySections { get; init; } = new();
    public List<StatementSectionResponse> EquitySections { get; init; } = new();

    /// <summary>Current-period Revenue − Expense, shown as its own equity line.</summary>
    public decimal CurrentEarnings { get; init; }

    public decimal TotalAssets { get; init; }
    public decimal TotalLiabilities { get; init; }
    public decimal TotalEquity { get; init; }
    public bool IsBalanced { get; init; }
}

// ---------------------------------------------------------------------------
// Income Statement (single period)
// ---------------------------------------------------------------------------

public record IncomeStatementResponse
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public bool IncludeInactiveAccounts { get; init; }
    public List<StatementSectionResponse> RevenueSections { get; init; } = new();
    public List<StatementSectionResponse> ExpenseSections { get; init; } = new();
    public decimal TotalRevenue { get; init; }
    public decimal TotalCostOfGoodsSold { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal TotalOperatingExpenses { get; init; }
    public decimal OperatingIncome { get; init; }
    public decimal TotalNonOperating { get; init; }
    public decimal NetIncome { get; init; }
}

// ---------------------------------------------------------------------------
// Cash Flow (indirect method, single period)
// ---------------------------------------------------------------------------

public record CashFlowAdjustmentResponse
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? AccountCode { get; init; }
}

public record CashFlowSectionResponse
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<CashFlowAdjustmentResponse> Adjustments { get; init; } = new();
    public decimal Subtotal { get; init; }
}

public record CashFlowStatementResponse
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public decimal NetIncome { get; init; }
    public CashFlowSectionResponse Operating { get; init; } = new() { Key = "operating", Title = "Operating Activities" };
    public CashFlowSectionResponse Investing { get; init; } = new() { Key = "investing", Title = "Investing Activities" };
    public CashFlowSectionResponse Financing { get; init; } = new() { Key = "financing", Title = "Financing Activities" };
    public decimal NetChangeInCash { get; init; }
    public decimal BeginningCash { get; init; }
    public decimal EndingCash { get; init; }

    /// <summary>True when EndingCash − BeginningCash == NetChangeInCash.</summary>
    public bool IsBalanced { get; init; }

    /// <summary>
    /// Balance-sheet accounts with Unclassified activity that moved in-window.
    /// Listed for auditability instead of silently dropped.
    /// </summary>
    public List<CashFlowAdjustmentResponse> Unmapped { get; init; } = new();
}
