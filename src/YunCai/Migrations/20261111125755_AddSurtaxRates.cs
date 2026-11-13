using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddSurtaxRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "city_build_tax_rate",
                table: "account_sets",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 7m);

            migrationBuilder.AddColumn<decimal>(
                name: "education_surcharge_rate",
                table: "account_sets",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 3m);

            migrationBuilder.AddColumn<decimal>(
                name: "local_education_surcharge_rate",
                table: "account_sets",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 2m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "city_build_tax_rate",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "education_surcharge_rate",
                table: "account_sets");

            migrationBuilder.DropColumn(
                name: "local_education_surcharge_rate",
                table: "account_sets");
        }
    }
}
