using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSetTaxLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tax_identity_type",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_login_account",
                table: "account_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_login_password_cipher",
                table: "account_sets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_login_type",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_region",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tax_identity_type",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "tax_login_account",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "tax_login_password_cipher",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "tax_login_type",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "tax_region",
                table: "account_sets");
        }
    }
}
