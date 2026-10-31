using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedAssetVoucherSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "voucher_id",
                table: "fixed_asset_changes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fixed_asset_voucher_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depreciation_expense_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    depreciation_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amortization_expense_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amortization_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    disposal_account_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_asset_voucher_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixed_asset_voucher_settings_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fixed_asset_voucher_settings_account_set_id",
                table: "fixed_asset_voucher_settings",
                column: "account_set_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixed_asset_voucher_settings");

            migrationBuilder.DropColumn(
                name: "voucher_id",
                table: "fixed_asset_changes");
        }
    }
}
