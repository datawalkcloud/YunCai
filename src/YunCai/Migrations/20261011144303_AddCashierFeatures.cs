using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_account_no",
                table: "journals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_name",
                table: "journals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "journals",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "smart_keywords",
                table: "entry_categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "voucher_subject_id",
                table: "entry_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    from_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_transfers_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_account_set_id_date",
                table: "transfers",
                columns: new[] { "account_set_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfers");

            migrationBuilder.DropColumn(
                name: "bank_account_no",
                table: "journals");

            migrationBuilder.DropColumn(
                name: "bank_name",
                table: "journals");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "journals");

            migrationBuilder.DropColumn(
                name: "smart_keywords",
                table: "entry_categories");

            migrationBuilder.DropColumn(
                name: "voucher_subject_id",
                table: "entry_categories");
        }
    }
}
