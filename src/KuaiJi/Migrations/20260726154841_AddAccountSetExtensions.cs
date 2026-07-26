using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSetExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "opening_balance",
                table: "accounts",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "closed_period",
                table: "account_sets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "income_tax_rate",
                table: "account_sets",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 25m);

            migrationBuilder.AddColumn<string>(
                name: "taxpayer_number",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "account_sets",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 13m);

            migrationBuilder.AddColumn<string>(
                name: "voucher_words",
                table: "account_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "记,收,付,转");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opening_balance",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "closed_period",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "income_tax_rate",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "taxpayer_number",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "voucher_words",
                table: "account_sets");
        }
    }
}
