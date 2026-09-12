using System.Text;
using AccountingEngine.Application.DTOs;

namespace AccountingEngine.Api;

/// <summary>
/// Flattens financial-statement DTOs into CSV (RFC 4180) for Excel / Google Sheets
/// consumption (e.g. <c>?format=csv</c>, <c>=IMPORTDATA(...)</c>, Power Query).
/// Output is UTF-8 with BOM so Excel opens it with correct encoding by default.
/// </summary>
public static class FinancialStatementCsvExporter
{
    public static byte[] BalanceSheet(BalanceSheetResponse bs)
    {
        var rows = new List<string[]>
        {
            new[] { "Balance Sheet", "As of", bs.AsOf.ToString("yyyy-MM-dd HH:mm 'UTC'") },
            new[] { "Section", "Code", "Account", "Amount", "Type" },
        };

        AppendSections(rows, bs.AssetSections);
        AppendTotal(rows, "Total Assets", bs.TotalAssets);
        AppendSections(rows, bs.LiabilitySections);
        AppendTotal(rows, "Total Liabilities", bs.TotalLiabilities);
        AppendSections(rows, bs.EquitySections);
        AppendTotal(rows, "Current Earnings (Revenue - Expense)", bs.CurrentEarnings);
        AppendTotal(rows, "Total Equity", bs.TotalEquity);
        AppendTotal(rows, "Total Liabilities and Equity", bs.TotalLiabilities + bs.TotalEquity);
        rows.Add(new[] { "Is Balanced", bs.IsBalanced.ToString() });

        return ToCsv(rows);
    }

    public static byte[] IncomeStatement(IncomeStatementResponse inc)
    {
        var rows = new List<string[]>
        {
            new[] { "Income Statement", "From", inc.From.ToString("yyyy-MM-dd"), "To", inc.To.ToString("yyyy-MM-dd") },
            new[] { "Section", "Code", "Account", "Amount", "Type" },
        };

        AppendSections(rows, inc.RevenueSections);
        AppendTotal(rows, "Total Revenue", inc.TotalRevenue);
        AppendSections(rows, inc.ExpenseSections.Where(s =>
            s.Key.Contains("cogs", StringComparison.OrdinalIgnoreCase)));
        AppendTotal(rows, "Total Cost of Goods Sold", inc.TotalCostOfGoodsSold);
        AppendTotal(rows, "Gross Profit", inc.GrossProfit);
        AppendSections(rows, inc.ExpenseSections.Where(s =>
            !s.Key.Contains("cogs", StringComparison.OrdinalIgnoreCase)));
        AppendTotal(rows, "Total Operating Expenses", inc.TotalOperatingExpenses);
        AppendTotal(rows, "Operating Income", inc.OperatingIncome);
        AppendTotal(rows, "Total Non-Operating", inc.TotalNonOperating);
        AppendTotal(rows, "Net Income", inc.NetIncome);

        return ToCsv(rows);
    }

    public static byte[] CashFlow(CashFlowStatementResponse cf)
    {
        var rows = new List<string[]>
        {
            new[] { "Statement of Cash Flows", "From", cf.From.ToString("yyyy-MM-dd"), "To", cf.To.ToString("yyyy-MM-dd") },
            new[] { "Section", "Label", "Account", "Amount" },
        };

        AppendSection(rows, cf.Operating);
        AppendSection(rows, cf.Investing);
        AppendSection(rows, cf.Financing);
        AppendTotal(rows, "Net Change in Cash", cf.NetChangeInCash);
        rows.Add(new[] { string.Empty, "Beginning Cash", string.Empty, Csv(cf.BeginningCash) });
        rows.Add(new[] { string.Empty, "Ending Cash", string.Empty, Csv(cf.EndingCash) });
        rows.Add(new[] { "Is Balanced", cf.IsBalanced.ToString() });

        if (cf.Unmapped.Count > 0)
        {
            rows.Add(new[] { "Unmapped (audit)", "Label", "Account", "Amount" });
            foreach (var u in cf.Unmapped)
                rows.Add(new[] { "unmapped", u.Label, u.AccountCode ?? string.Empty, Csv(u.Amount) });
        }

        return ToCsv(rows);
    }

    // ---- helpers ----------------------------------------------------------

    private static void AppendSections(List<string[]> rows, IEnumerable<StatementSectionResponse> sections)
    {
        foreach (var section in sections)
            AppendSection(rows, section);
    }

    private static void AppendSection(List<string[]> rows, StatementSectionResponse section)
    {
        rows.Add(new[] { section.Title, string.Empty, string.Empty, string.Empty });
        foreach (var line in section.Lines)
            rows.Add(new[]
            {
                line.IsHeader ? string.Empty : section.Title,
                line.AccountCode,
                (line.IsHeader ? "[H] " : string.Empty) + line.AccountName + (line.IsContra ? " (contra)" : string.Empty),
                Csv(line.Amount),
            });
        rows.Add(new[] { string.Empty, string.Empty, $"Subtotal - {section.Title}", Csv(section.Subtotal) });
    }

    private static void AppendSection(List<string[]> rows, CashFlowSectionResponse section)
    {
        rows.Add(new[] { section.Title, string.Empty, string.Empty, string.Empty });
        foreach (var adj in section.Adjustments)
            rows.Add(new[] { section.Title, adj.Label, adj.AccountCode ?? string.Empty, Csv(adj.Amount) });
        rows.Add(new[] { string.Empty, $"Subtotal - {section.Title}", string.Empty, Csv(section.Subtotal) });
    }

    private static void AppendTotal(List<string[]> rows, string label, decimal amount) =>
        rows.Add(new[] { string.Empty, string.Empty, $"= {label}", Csv(amount) });

    private static string Csv(decimal value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>RFC 4180: quote fields containing comma, quote, newline; double embedded quotes.</summary>
    private static string Encode(string? field)
    {
        field ??= string.Empty;
        if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }

    private static byte[] ToCsv(List<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', row.Select(Encode)));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }
}
