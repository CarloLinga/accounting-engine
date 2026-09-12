using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialStatementClassificationToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "balance_sheet_class",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cash_flow_activity",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "income_statement_class",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_cash_equivalent",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_contra",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_postable",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_account_id",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "statement",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill classification for rows created before this migration:
            // keep double-entry semantics consistent with the service defaults.
            migrationBuilder.Sql(@"
                UPDATE accounts SET statement = CASE type
                    WHEN 'Revenue' THEN 'IncomeStatement'
                    WHEN 'Expense' THEN 'IncomeStatement'
                    ELSE 'BalanceSheet' END
                WHERE statement = '';
                UPDATE accounts SET balance_sheet_class = CASE type
                    WHEN 'Asset' THEN 'CurrentAsset'
                    WHEN 'Liability' THEN 'CurrentLiability'
                    WHEN 'Equity' THEN 'Equity'
                    ELSE NULL END
                WHERE balance_sheet_class IS NULL AND type IN ('Asset','Liability','Equity');
                UPDATE accounts SET income_statement_class = CASE type
                    WHEN 'Revenue' THEN 'OperatingRevenue'
                    WHEN 'Expense' THEN 'OperatingExpense'
                    ELSE NULL END
                WHERE income_statement_class IS NULL AND type IN ('Revenue','Expense');
                UPDATE accounts SET is_postable = TRUE WHERE is_postable = FALSE;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_parent_account_id_display_order",
                table: "accounts",
                columns: new[] { "parent_account_id", "display_order" });

            migrationBuilder.AddForeignKey(
                name: "fk_accounts_accounts_parent_account_id",
                table: "accounts",
                column: "parent_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_accounts_accounts_parent_account_id",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_parent_account_id_display_order",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "balance_sheet_class",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "cash_flow_activity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "income_statement_class",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_cash_equivalent",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_contra",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_postable",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "parent_account_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "statement",
                table: "accounts");
        }
    }
}
