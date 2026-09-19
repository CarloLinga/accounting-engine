using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_journals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    customer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tax_id_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    vatable_sale = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    zero_rated_sale = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    vat_exempt_sale = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_journals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_journals_invoice_no",
                table: "sales_journals",
                column: "invoice_no",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_journals");
        }
    }
}
