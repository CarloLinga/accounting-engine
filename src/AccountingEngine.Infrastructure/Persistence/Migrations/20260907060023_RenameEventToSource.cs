using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameEventToSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_rule_lines");

            migrationBuilder.DropTable(
                name: "event_rules");

            migrationBuilder.CreateTable(
                name: "source_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_manual_entry_allowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_rule_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_rule_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_rule_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_rule_lines_source_rules_source_rule_id",
                        column: x => x.source_rule_id,
                        principalTable: "source_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_source_rule_lines_account_id",
                table: "source_rule_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_rule_lines_source_rule_id",
                table: "source_rule_lines",
                column: "source_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_rules_source_type",
                table: "source_rules",
                column: "source_type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_rule_lines");

            migrationBuilder.DropTable(
                name: "source_rules");

            migrationBuilder.CreateTable(
                name: "event_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_manual_entry_allowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_rule_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entry_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_rule_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_rule_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_rule_lines_event_rules_event_rule_id",
                        column: x => x.event_rule_id,
                        principalTable: "event_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_rule_lines_account_id",
                table: "event_rule_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_rule_lines_event_rule_id",
                table: "event_rule_lines",
                column: "event_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_rules_event_type",
                table: "event_rules",
                column: "event_type",
                unique: true);
        }
    }
}
