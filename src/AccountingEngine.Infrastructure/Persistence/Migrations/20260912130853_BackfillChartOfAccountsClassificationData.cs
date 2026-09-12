using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillChartOfAccountsClassificationData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The schema migration 20260912124636 defaulted every account by
            // type (all Expenses -> OperatingExpense, all Assets -> CurrentAsset,
            // nothing flagged). This migration enriches the STANDARD seed chart
            // of accounts (see Seeder/ChartOfAccounts.cs) so existing databases
            // match what a fresh seed produces.
            migrationBuilder.Sql(@"
                -- Income-statement subclass fixes.
                UPDATE accounts SET income_statement_class = 'CostOfGoodsSold'     WHERE code IN ('5000','5010');
                UPDATE accounts SET income_statement_class = 'ContraRevenue'       WHERE code IN ('4100');
                UPDATE accounts SET income_statement_class = 'NonOperatingRevenue' WHERE code IN ('4200','4300');
                UPDATE accounts SET income_statement_class = 'NonOperatingExpense' WHERE code IN ('5900','5910');

                -- Contra accounts net against their section via negative balances.
                UPDATE accounts SET is_contra = TRUE WHERE code IN ('1110','1510','1610','1710','3100','4100');

                -- Cash pool for the statement of cash flows.
                UPDATE accounts SET is_cash_equivalent = TRUE WHERE code IN ('1000','1010','1020');

                -- Cash-flow activity by section (indirect method).
                UPDATE accounts SET cash_flow_activity = 'Investing' WHERE code IN ('1500','1510','1600','1610','1700','1710');
                UPDATE accounts SET cash_flow_activity = 'Financing' WHERE code IN ('2300','3000','3100','3200');
                UPDATE accounts SET cash_flow_activity = 'Operating'
                    WHERE statement = 'BalanceSheet'
                      AND cash_flow_activity = ''
                      AND is_cash_equivalent = FALSE;
                UPDATE accounts SET cash_flow_activity = 'Unclassified'
                    WHERE is_cash_equivalent = TRUE;

                -- Report header accounts (non-postable grouping rows).
                INSERT INTO accounts (id, code, name, type, is_active, created_at, updated_at,
                                      parent_account_id, is_postable, statement, balance_sheet_class,
                                      income_statement_class, cash_flow_activity, is_cash_equivalent,
                                      is_contra, display_order)
                VALUES
                    ('10000000-0000-4000-8000-000000000001', '1000-H', 'Cash and Cash Equivalents', 'Asset', TRUE, CURRENT_TIMESTAMP, NULL, NULL, FALSE, 'BalanceSheet', 'CurrentAsset', NULL, 'Unclassified', FALSE, FALSE, 1),
                    ('10000000-0000-4000-8000-000000000011', '1100-H', 'Receivables',                'Asset', TRUE, CURRENT_TIMESTAMP, NULL, NULL, FALSE, 'BalanceSheet', 'CurrentAsset', NULL, 'Operating',    FALSE, FALSE, 10),
                    ('10000000-0000-4000-8000-000000000012', '1200-H', 'Inventories',                'Asset', TRUE, CURRENT_TIMESTAMP, NULL, NULL, FALSE, 'BalanceSheet', 'CurrentAsset', NULL, 'Operating',    FALSE, FALSE, 20),
                    ('10000000-0000-4000-8000-000000000013', '1300-H', 'Prepayments',                'Asset', TRUE, CURRENT_TIMESTAMP, NULL, NULL, FALSE, 'BalanceSheet', 'CurrentAsset', NULL, 'Operating',    FALSE, FALSE, 30),
                    ('10000000-0000-4000-8000-000000000015', '1500-H', 'Property and Equipment',     'Asset', TRUE, CURRENT_TIMESTAMP, NULL, NULL, FALSE, 'BalanceSheet', 'NonCurrentAsset', NULL, 'Investing', FALSE, FALSE, 40);

                -- Wire detail accounts under their header.
                UPDATE accounts SET parent_account_id = '10000000-0000-4000-8000-000000000001' WHERE code IN ('1000','1010','1020');
                UPDATE accounts SET parent_account_id = '10000000-0000-4000-8000-000000000011' WHERE code IN ('1100','1110');
                UPDATE accounts SET parent_account_id = '10000000-0000-4000-8000-000000000012' WHERE code IN ('1200','1210','1220');
                UPDATE accounts SET parent_account_id = '10000000-0000-4000-8000-000000000013' WHERE code IN ('1300','1310');
                UPDATE accounts SET parent_account_id = '10000000-0000-4000-8000-000000000015' WHERE code IN ('1500','1510','1600','1610','1700','1710');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE accounts SET parent_account_id = NULL
                WHERE parent_account_id IN ('10000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000011','10000000-0000-4000-8000-000000000012','10000000-0000-4000-8000-000000000013','10000000-0000-4000-8000-000000000015');

                DELETE FROM accounts WHERE code IN ('1000-H','1100-H','1200-H','1300-H','1500-H');

                UPDATE accounts SET is_cash_equivalent = FALSE, is_contra = FALSE;
                UPDATE accounts SET cash_flow_activity = '';
                UPDATE accounts SET income_statement_class = NULL
                WHERE code IN ('5000','5010','4100','4200','4300','5900','5910');
            ");
        }
    }
}
