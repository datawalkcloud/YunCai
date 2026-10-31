using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bill_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expense_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    settle_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    deductible_tax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_bills", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_bills_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expense_bills_account_set_id_bill_date",
                table: "expense_bills",
                columns: new[] { "account_set_id", "bill_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expense_bills_account_set_id_bill_no",
                table: "expense_bills",
                columns: new[] { "account_set_id", "bill_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_bills");
        }
    }
}
