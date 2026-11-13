using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeTaxProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "deduct_basic_expense",
                table: "employees",
                type: "boolean",
                nullable: false,
                // 存量员工按柠檬云口径回填「是」：默认扣减除费用。
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "education",
                table: "employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "employment_type",
                table: "employees",
                type: "integer",
                nullable: false,
                // 1 = 雇员（EmployeeEmploymentType.Employee），存量员工一律按雇员回填。
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_disabled",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_martyr_family",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_senior_expert",
                table: "employees",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_solo_elder",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "pay_start_date",
                table: "employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "pays_social_insurance",
                table: "employees",
                type: "boolean",
                nullable: false,
                // 存量员工默认「缴纳五险一金」，与新增表单初始值一致。
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "employee_insurances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_insurances", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_insurances_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_insurances_insurance_items_insurance_item_id",
                        column: x => x.insurance_item_id,
                        principalTable: "insurance_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_insurances_employee_id_insurance_item_id",
                table: "employee_insurances",
                columns: new[] { "employee_id", "insurance_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_insurances_insurance_item_id",
                table: "employee_insurances",
                column: "insurance_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_insurances");

            migrationBuilder.DropColumn(
                name: "deduct_basic_expense",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "education",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "employment_type",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "is_disabled",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "is_martyr_family",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "is_senior_expert",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "is_solo_elder",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "pay_start_date",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "pays_social_insurance",
                table: "employees");
        }
    }
}
