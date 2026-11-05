using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSetEditParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "assets_module_enabled",
                table: "account_sets",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "balance_assist_scope",
                table: "account_sets",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "funds_module_enabled",
                table: "account_sets",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "iit_channel",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "iit_login_account",
                table: "account_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "iit_login_password_cipher",
                table: "account_sets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "iit_login_type",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_export_enterprise",
                table: "account_sets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "unit_decimal_places",
                table: "account_sets",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<bool>(
                name: "voucher_audit_required",
                table: "account_sets",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assets_module_enabled",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "balance_assist_scope",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "funds_module_enabled",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "iit_channel",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "iit_login_account",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "iit_login_password_cipher",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "iit_login_type",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "industry",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "is_export_enterprise",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "unit_decimal_places",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "voucher_audit_required",
                table: "account_sets");
        }
    }
}
