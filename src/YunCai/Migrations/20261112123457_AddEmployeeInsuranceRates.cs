using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeInsuranceRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "company_rate",
                table: "employee_insurances",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "personal_rate",
                table: "employee_insurances",
                type: "numeric(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "company_rate",
                table: "employee_insurances");

            migrationBuilder.DropColumn(
                name: "personal_rate",
                table: "employee_insurances");
        }
    }
}
