using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryVoucherSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "salary_voucher_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payroll_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tax_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    insurance_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salary_voucher_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_salary_voucher_settings_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_salary_voucher_settings_account_set_id",
                table: "salary_voucher_settings",
                column: "account_set_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "salary_voucher_settings");
        }
    }
}
