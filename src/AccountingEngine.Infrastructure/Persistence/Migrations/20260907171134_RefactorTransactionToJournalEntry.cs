using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTransactionToJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_journal_entry_lines_transactions_transaction_id",
                table: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_journal_entry_lines_transaction_id_sequence",
                table: "journal_entry_lines");

            migrationBuilder.RenameColumn(
                name: "transaction_id",
                table: "journal_entry_lines",
                newName: "journal_entry_id");

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_id_sequence",
                table: "journal_entry_lines",
                columns: new[] { "id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_journal_entry_id",
                table: "journal_entry_lines",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_posted_at",
                table: "journal_entries",
                column: "posted_at");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_reference",
                table: "journal_entries",
                column: "reference",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_journal_entry_lines_journal_entries_journal_entry_id",
                table: "journal_entry_lines",
                column: "journal_entry_id",
                principalTable: "journal_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_journal_entry_lines_journal_entries_journal_entry_id",
                table: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ix_journal_entry_lines_id_sequence",
                table: "journal_entry_lines");

            migrationBuilder.DropIndex(
                name: "ix_journal_entry_lines_journal_entry_id",
                table: "journal_entry_lines");

            migrationBuilder.RenameColumn(
                name: "journal_entry_id",
                table: "journal_entry_lines",
                newName: "transaction_id");

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_transaction_id_sequence",
                table: "journal_entry_lines",
                columns: new[] { "transaction_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_posted_at",
                table: "transactions",
                column: "posted_at");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_reference",
                table: "transactions",
                column: "reference");

            migrationBuilder.AddForeignKey(
                name: "fk_journal_entry_lines_transactions_transaction_id",
                table: "journal_entry_lines",
                column: "transaction_id",
                principalTable: "transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
