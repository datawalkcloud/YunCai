using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddLemonModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archive_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    doc_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    related_doc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    voucher_text = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    matter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    check_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    uploader = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_archive_items_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    bill_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    drawer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acceptor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transferable = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => x.id);
                    table.ForeignKey(
                        name: "FK_bills_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_currencies_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    id_type = table.Column<int>(type: "integer", nullable: false),
                    id_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    dept = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    leave_date = table.Column<DateOnly>(type: "date", nullable: true),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bank_account_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    post = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    title = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_employees_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fixed_asset_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    attr = table.Column<int>(type: "integer", nullable: false),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    months = table.Column<int>(type: "integer", nullable: false),
                    residual_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_asset_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixed_asset_categories_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fixed_asset_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    before = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    after = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_asset_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fixed_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dept = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    origin_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    accumulated_depreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    last_depreciated_period = table.Column<int>(type: "integer", nullable: false),
                    cleared_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixed_assets_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "insurance_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    company_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    personal_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurance_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_insurance_items_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    invoice_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_date = table.Column<DateOnly>(type: "date", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    partner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    biz_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    auth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tax_period = table.Column<int>(type: "integer", nullable: true),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    log = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gross = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    deductible = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    personal_deduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_threshold = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    adjust = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    company_part = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    accrued_voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salary_bills", x => x.id);
                    table.ForeignKey(
                        name: "FK_salary_bills_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_templates_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voucher_words",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    print_title = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_words", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_words_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    goods_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_template_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_debit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_template_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_template_lines_voucher_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "voucher_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_archive_items_account_set_id_is_archived",
                table: "archive_items",
                columns: new[] { "account_set_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "IX_bills_account_set_id_bill_no",
                table: "bills",
                columns: new[] { "account_set_id", "bill_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bills_account_set_id_issue_date",
                table: "bills",
                columns: new[] { "account_set_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_currencies_account_set_id_code",
                table: "currencies",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_account_set_id_code",
                table: "employees",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixed_asset_categories_account_set_id_code",
                table: "fixed_asset_categories",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixed_asset_changes_account_set_id_asset_id",
                table: "fixed_asset_changes",
                columns: new[] { "account_set_id", "asset_id" });

            migrationBuilder.CreateIndex(
                name: "IX_fixed_asset_changes_account_set_id_period",
                table: "fixed_asset_changes",
                columns: new[] { "account_set_id", "period" });

            migrationBuilder.CreateIndex(
                name: "IX_fixed_assets_account_set_id_code",
                table: "fixed_assets",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixed_assets_category_id",
                table: "fixed_assets",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_insurance_items_account_set_id_name",
                table: "insurance_items",
                columns: new[] { "account_set_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_invoice_id",
                table: "invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_account_set_id_direction_invoice_no",
                table: "invoices",
                columns: new[] { "account_set_id", "direction", "invoice_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_account_set_id_direction_issue_date",
                table: "invoices",
                columns: new[] { "account_set_id", "direction", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_account_set_id_status",
                table: "invoices",
                columns: new[] { "account_set_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_operation_logs_account_set_id_time",
                table: "operation_logs",
                columns: new[] { "account_set_id", "time" });

            migrationBuilder.CreateIndex(
                name: "IX_salary_bills_account_set_id_period_employee_id",
                table: "salary_bills",
                columns: new[] { "account_set_id", "period", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_template_lines_template_id",
                table: "voucher_template_lines",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_templates_account_set_id_name",
                table: "voucher_templates",
                columns: new[] { "account_set_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_words_account_set_id_is_default",
                table: "voucher_words",
                columns: new[] { "account_set_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_words_account_set_id_word",
                table: "voucher_words",
                columns: new[] { "account_set_id", "word" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archive_items");

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "fixed_asset_categories");

            migrationBuilder.DropTable(
                name: "fixed_asset_changes");

            migrationBuilder.DropTable(
                name: "fixed_assets");

            migrationBuilder.DropTable(
                name: "insurance_items");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "operation_logs");

            migrationBuilder.DropTable(
                name: "salary_bills");

            migrationBuilder.DropTable(
                name: "voucher_template_lines");

            migrationBuilder.DropTable(
                name: "voucher_words");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "voucher_templates");
        }
    }
}
