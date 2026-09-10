using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignDebitCreditCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_debit_xor_credit",
                table: "journal_entry_lines");

            migrationBuilder.AddCheckConstraint(
                name: "chk_debit_xor_credit",
                table: "journal_entry_lines",
                sql: "(CAST(debit AS NUMERIC) > 0 AND CAST(credit AS NUMERIC) = 0) OR (CAST(credit AS NUMERIC) > 0 AND CAST(debit AS NUMERIC) = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_debit_xor_credit",
                table: "journal_entry_lines");

            migrationBuilder.AddCheckConstraint(
                name: "chk_debit_xor_credit",
                table: "journal_entry_lines",
                sql: "(debit > 0 AND credit = 0) OR (credit > 0 AND debit = 0)");
        }
    }
}
