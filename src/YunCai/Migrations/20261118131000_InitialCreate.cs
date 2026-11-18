using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    standard = table.Column<int>(type: "integer", nullable: false),
                    taxpayer_type = table.Column<int>(type: "integer", nullable: false),
                    base_currency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_year = table.Column<int>(type: "integer", nullable: false),
                    start_month = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    account_coding_scheme = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "4-2-2-2"),
                    taxpayer_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 13m),
                    income_tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 25m),
                    city_build_tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 7m),
                    education_surcharge_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 3m),
                    local_education_surcharge_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 2m),
                    tax_region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tax_login_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tax_identity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tax_login_account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_login_password_cipher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voucher_words = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "记,收,付,转"),
                    enabled_assist_types = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    industry = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    assets_module_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    funds_module_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    voucher_audit_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    balance_assist_scope = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    unit_decimal_places = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    is_export_enterprise = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    iit_channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    iit_login_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    iit_login_account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    iit_login_password_cipher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_period = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    carried_period = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "budgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budgets", x => x.id);
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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_asset_changes", x => x.id);
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
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    before_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    after_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "preset_accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    standard = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    parent_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    order_no = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preset_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_admin = table.Column<bool>(type: "boolean", nullable: false),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "account_set_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    permissions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_set_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_set_roles_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    sub_category = table.Column<int>(type: "integer", nullable: true),
                    is_cash_account = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_quantity_accounted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_foreign_currency_accounted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    is_leaf = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_preset = table.Column<bool>(type: "boolean", nullable: false),
                    mnemonic = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_accounts_parent_id",
                        column: x => x.parent_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "archive_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_archive_categories_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_archive_categories_archive_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "archive_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "archive_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    is_paper = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "assist_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    is_leaf = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_assist_items_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assist_items_assist_items_parent_id",
                        column: x => x.parent_id,
                        principalTable: "assist_items",
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
                    payee = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acceptor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    drawer_account_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    payee_account_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    acceptor_account_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    drawer_bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payee_bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acceptor_bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transferable = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "cash_flow_formulas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    expression = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_flow_formulas", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_flow_formulas_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "closing_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closing_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_closing_templates_account_sets_account_set_id",
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
                    convert_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
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
                name: "custom_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_type = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_reports_account_sets_account_set_id",
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
                    education = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_martyr_family = table.Column<bool>(type: "boolean", nullable: false),
                    is_solo_elder = table.Column<bool>(type: "boolean", nullable: false),
                    is_senior_expert = table.Column<bool>(type: "boolean", nullable: true),
                    employment_type = table.Column<int>(type: "integer", nullable: false),
                    deduct_basic_expense = table.Column<bool>(type: "boolean", nullable: false),
                    pay_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pays_social_insurance = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "entry_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_flow_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    smart_keywords = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    voucher_subject_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entry_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_entry_categories_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    is_initial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
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
                name: "journals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "人民币"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    bank_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bank_account_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journals", x => x.id);
                    table.ForeignKey(
                        name: "FK_journals_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "tax_portal_sessions",
                columns: table => new
                {
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    portal_kind = table.Column<int>(type: "integer", nullable: false),
                    payload_cipher = table.Column<string>(type: "text", nullable: false),
                    saved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_portal_sessions", x => new { x.account_set_id, x.user_id, x.portal_kind });
                    table.ForeignKey(
                        name: "FK_tax_portal_sessions_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    suggestion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tax_tasks_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    from_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_transfers_account_sets_account_set_id",
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
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    attachment_count = table.Column<int>(type: "integer", nullable: false),
                    remark = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    preparer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "FK_vouchers_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    m01 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m02 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m03 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m04 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m05 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m06 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m07 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m08 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m09 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m10 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m11 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    m12 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_budget_lines_budgets_budget_id",
                        column: x => x.budget_id,
                        principalTable: "budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_set_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_owner = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_set_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_set_members_account_set_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "account_set_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_set_members_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_assist_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_assist_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_assist_types_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_currencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_currencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_currencies_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bill_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_bill_attachments_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "closing_template_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closing_template_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_closing_template_lines_closing_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "closing_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_insurances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insurance_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    company_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    personal_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true)
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
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    counterparty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    project = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    expense = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settlement_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bill_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    remark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entries_journals_journal_id",
                        column: x => x.journal_id,
                        principalTable: "journals",
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

            migrationBuilder.CreateTable(
                name: "voucher_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_attachments_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_entries_vouchers_voucher_id",
                        column: x => x.voucher_id,
                        principalTable: "vouchers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voucher_entry_assists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    assist_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_entry_assists", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_entry_assists_assist_items_assist_item_id",
                        column: x => x.assist_item_id,
                        principalTable: "assist_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_entry_assists_voucher_entries_voucher_entry_id",
                        column: x => x.voucher_entry_id,
                        principalTable: "voucher_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "preset_accounts",
                columns: new[] { "id", "category", "code", "direction", "name", "order_no", "parent_code", "standard" },
                values: new object[,]
                {
                    { 1, 1, "1001", 1, "库存现金", 1, null, 1 },
                    { 2, 1, "1002", 1, "银行存款", 2, null, 1 },
                    { 3, 1, "1003", 1, "存放中央银行款项", 3, null, 1 },
                    { 4, 1, "1011", 1, "存放同业", 4, null, 1 },
                    { 5, 1, "1012", 1, "其他货币资金", 5, null, 1 },
                    { 6, 1, "1021", 1, "结算备付金", 6, null, 1 },
                    { 7, 1, "1031", 1, "存出保证金", 7, null, 1 },
                    { 8, 1, "1101", 1, "交易性金融资产", 8, null, 1 },
                    { 9, 1, "1111", 1, "买入返售金融资产", 9, null, 1 },
                    { 10, 1, "1121", 1, "应收票据", 10, null, 1 },
                    { 11, 1, "1122", 1, "应收账款", 11, null, 1 },
                    { 12, 1, "1123", 1, "预付账款", 12, null, 1 },
                    { 13, 1, "1131", 1, "应收股利", 13, null, 1 },
                    { 14, 1, "1132", 1, "应收利息", 14, null, 1 },
                    { 15, 1, "1201", 1, "应收代位追偿款", 15, null, 1 },
                    { 16, 1, "1211", 1, "应收分保账款", 16, null, 1 },
                    { 17, 1, "1212", 1, "应收分保合同准备金", 17, null, 1 },
                    { 18, 1, "1221", 1, "其他应收款", 18, null, 1 },
                    { 19, 1, "1231", 2, "坏账准备", 19, null, 1 },
                    { 20, 1, "1231001", 2, "计提应收账款准备", 20, "1231", 1 },
                    { 21, 1, "1231002", 2, "计提其他应收款准备", 21, "1231", 1 },
                    { 22, 1, "1301", 1, "贴现资产", 22, null, 1 },
                    { 23, 1, "1302", 1, "拆出资金", 23, null, 1 },
                    { 24, 1, "1303", 1, "贷款", 24, null, 1 },
                    { 25, 1, "1304", 2, "贷款损失准备", 25, null, 1 },
                    { 26, 1, "1311", 1, "代理兑付证券", 26, null, 1 },
                    { 27, 1, "1321", 1, "代理业务资产", 27, null, 1 },
                    { 28, 1, "1401", 1, "材料采购", 28, null, 1 },
                    { 29, 1, "1402", 1, "在途物资", 29, null, 1 },
                    { 30, 1, "1403", 1, "原材料", 30, null, 1 },
                    { 31, 1, "1404", 1, "材料成本差异", 31, null, 1 },
                    { 32, 1, "1405", 1, "库存商品", 32, null, 1 },
                    { 33, 1, "1406", 1, "发出商品", 33, null, 1 },
                    { 34, 1, "1407", 2, "商品进销差价", 34, null, 1 },
                    { 35, 1, "1408", 1, "委托加工物资", 35, null, 1 },
                    { 36, 1, "1411", 1, "周转材料", 36, null, 1 },
                    { 37, 1, "1421", 1, "消耗性生物资产", 37, null, 1 },
                    { 38, 1, "1431", 1, "贵金属", 38, null, 1 },
                    { 39, 1, "1441", 1, "抵债资产", 39, null, 1 },
                    { 40, 1, "1451", 1, "损余物资", 40, null, 1 },
                    { 41, 1, "1461", 1, "融资租赁资产", 41, null, 1 },
                    { 42, 1, "1471", 2, "存货跌价准备", 42, null, 1 },
                    { 43, 1, "1501", 1, "持有至到期投资", 43, null, 1 },
                    { 44, 1, "1502", 2, "持有至到期投资减值准备", 44, null, 1 },
                    { 45, 1, "1503", 1, "可供出售金融资产", 45, null, 1 },
                    { 46, 1, "1511", 1, "长期股权投资", 46, null, 1 },
                    { 47, 1, "1512", 2, "长期股权投资减值准备", 47, null, 1 },
                    { 48, 1, "1521", 1, "投资性房地产", 48, null, 1 },
                    { 49, 1, "1531", 1, "长期应收款", 49, null, 1 },
                    { 50, 1, "1532", 2, "未实现融资收益", 50, null, 1 },
                    { 51, 1, "1541", 1, "存出资本保证金", 51, null, 1 },
                    { 52, 1, "1601", 1, "固定资产", 52, null, 1 },
                    { 53, 1, "1602", 2, "累计折旧", 53, null, 1 },
                    { 54, 1, "1603", 2, "固定资产减值准备", 54, null, 1 },
                    { 55, 1, "1604", 1, "在建工程", 55, null, 1 },
                    { 56, 1, "1605", 1, "工程物资", 56, null, 1 },
                    { 57, 1, "1606", 1, "固定资产清理", 57, null, 1 },
                    { 58, 1, "1611", 1, "未担保余值", 58, null, 1 },
                    { 59, 1, "1621", 1, "生产性生物资产", 59, null, 1 },
                    { 60, 1, "1622", 2, "生产性生物资产累计折旧", 60, null, 1 },
                    { 61, 1, "1623", 1, "公益性生物资产", 61, null, 1 },
                    { 62, 1, "1631", 1, "油气资产", 62, null, 1 },
                    { 63, 1, "1632", 2, "累计折耗", 63, null, 1 },
                    { 64, 1, "1701", 1, "无形资产", 64, null, 1 },
                    { 65, 1, "1702", 2, "累计摊销", 65, null, 1 },
                    { 66, 1, "1703", 2, "无形资产减值准备", 66, null, 1 },
                    { 67, 1, "1711", 1, "商誉", 67, null, 1 },
                    { 68, 1, "1801", 1, "长期待摊费用", 68, null, 1 },
                    { 69, 1, "1811", 1, "递延所得税资产", 69, null, 1 },
                    { 70, 1, "1821", 1, "独立账户资产", 70, null, 1 },
                    { 71, 1, "1901", 1, "待处理财产损溢", 71, null, 1 },
                    { 72, 2, "2001", 2, "短期借款", 72, null, 1 },
                    { 73, 2, "2002", 2, "存入保证金", 73, null, 1 },
                    { 74, 2, "2003", 2, "拆入资金", 74, null, 1 },
                    { 75, 2, "2004", 2, "向中央银行借款", 75, null, 1 },
                    { 76, 2, "2011", 2, "吸收存款", 76, null, 1 },
                    { 77, 2, "2012", 2, "同业存放", 77, null, 1 },
                    { 78, 2, "2021", 2, "贴现负债", 78, null, 1 },
                    { 79, 2, "2101", 2, "交易性金融负债", 79, null, 1 },
                    { 80, 2, "2111", 2, "卖出回购金融资产款", 80, null, 1 },
                    { 81, 2, "2201", 2, "应付票据", 81, null, 1 },
                    { 82, 2, "2202", 2, "应付账款", 82, null, 1 },
                    { 83, 2, "2203", 2, "预收账款", 83, null, 1 },
                    { 84, 2, "2211", 2, "应付职工薪酬", 84, null, 1 },
                    { 85, 2, "2221", 2, "应交税费", 85, null, 1 },
                    { 86, 2, "2221001", 2, "应交增值税", 86, "2221", 1 },
                    { 87, 2, "222100101", 1, "进项税额", 87, "2221001", 1 },
                    { 88, 2, "222100102", 1, "已交税金", 88, "2221001", 1 },
                    { 89, 2, "222100103", 1, "转出未交增值税", 89, "2221001", 1 },
                    { 90, 2, "222100104", 1, "减免税款", 90, "2221001", 1 },
                    { 91, 2, "222100105", 2, "销项税额", 91, "2221001", 1 },
                    { 92, 2, "222100106", 2, "出口退税", 92, "2221001", 1 },
                    { 93, 2, "222100107", 2, "进项税额转出", 93, "2221001", 1 },
                    { 94, 2, "222100108", 1, "出口抵减内销产品应纳税额", 94, "2221001", 1 },
                    { 95, 2, "222100109", 2, "转出多交增值税", 95, "2221001", 1 },
                    { 96, 2, "222100110", 1, "销项税额抵减", 96, "2221001", 1 },
                    { 97, 2, "2221002", 2, "未交增值税", 97, "2221", 1 },
                    { 98, 2, "2221003", 2, "应交消费税", 98, "2221", 1 },
                    { 99, 2, "2221004", 2, "应交营业税", 99, "2221", 1 },
                    { 100, 2, "2221005", 2, "应交资源税", 100, "2221", 1 },
                    { 101, 2, "2221006", 2, "应交所得税", 101, "2221", 1 },
                    { 102, 2, "2221007", 2, "应交土地增值税", 102, "2221", 1 },
                    { 103, 2, "2221008", 2, "应交城市维护建设税", 103, "2221", 1 },
                    { 104, 2, "2221009", 2, "应交房产税", 104, "2221", 1 },
                    { 105, 2, "2221010", 2, "应交城镇土地使用税", 105, "2221", 1 },
                    { 106, 2, "2221011", 2, "应交车船使用税", 106, "2221", 1 },
                    { 107, 2, "2221012", 2, "应交个人所得税", 107, "2221", 1 },
                    { 108, 2, "2221013", 2, "教育费附加", 108, "2221", 1 },
                    { 109, 2, "2221014", 2, "地方教育费附加", 109, "2221", 1 },
                    { 110, 2, "2221015", 2, "矿产资源补偿费", 110, "2221", 1 },
                    { 111, 2, "2221016", 2, "排污费", 111, "2221", 1 },
                    { 112, 2, "2221017", 2, "印花税", 112, "2221", 1 },
                    { 113, 2, "2221018", 1, "预交增值税", 113, "2221", 1 },
                    { 114, 2, "2221019", 1, "待抵扣进项税额", 114, "2221", 1 },
                    { 115, 2, "2221020", 1, "待认证进项税额", 115, "2221", 1 },
                    { 116, 2, "2221021", 2, "待转销项税额", 116, "2221", 1 },
                    { 117, 2, "2221022", 1, "增值税留抵税额", 117, "2221", 1 },
                    { 118, 2, "2221023", 2, "简易计税", 118, "2221", 1 },
                    { 119, 2, "2221024", 2, "转让金融商品应交增值税", 119, "2221", 1 },
                    { 120, 2, "2221025", 2, "代扣代交增值税", 120, "2221", 1 },
                    { 121, 2, "2231", 2, "应付利息", 121, null, 1 },
                    { 122, 2, "2232", 2, "应付股利", 122, null, 1 },
                    { 123, 2, "2241", 2, "其他应付款", 123, null, 1 },
                    { 124, 2, "2251", 2, "应付保单红利", 124, null, 1 },
                    { 125, 2, "2261", 2, "应付分保账款", 125, null, 1 },
                    { 126, 2, "2311", 2, "代理买卖证券款", 126, null, 1 },
                    { 127, 2, "2312", 2, "代理承销证券款", 127, null, 1 },
                    { 128, 2, "2313", 2, "代理兑付证券款", 128, null, 1 },
                    { 129, 2, "2314", 2, "代理业务负债", 129, null, 1 },
                    { 130, 2, "2401", 2, "递延收益", 130, null, 1 },
                    { 131, 2, "2501", 2, "长期借款", 131, null, 1 },
                    { 132, 2, "2502", 2, "应付债券", 132, null, 1 },
                    { 133, 2, "2502001", 2, "优先股", 133, "2502", 1 },
                    { 134, 2, "2502002", 2, "永续股", 134, "2502", 1 },
                    { 135, 2, "2601", 2, "未到期责任准备金", 135, null, 1 },
                    { 136, 2, "2602", 2, "保险责任准备金", 136, null, 1 },
                    { 137, 2, "2611", 2, "保户储金", 137, null, 1 },
                    { 138, 2, "2621", 2, "独立账户负债", 138, null, 1 },
                    { 139, 2, "2701", 2, "长期应付款", 139, null, 1 },
                    { 140, 2, "2702", 1, "未确认融资费用", 140, null, 1 },
                    { 141, 2, "2711", 2, "专项应付款", 141, null, 1 },
                    { 142, 2, "2801", 2, "预计负债", 142, null, 1 },
                    { 143, 2, "2901", 2, "递延所得税负债", 143, null, 1 },
                    { 144, 1, "3001", 1, "清算资金往来", 144, null, 1 },
                    { 145, 1, "3002", 1, "货币兑换", 145, null, 1 },
                    { 146, 1, "3101", 1, "衍生工具", 146, null, 1 },
                    { 147, 1, "3201", 1, "套期工具", 147, null, 1 },
                    { 148, 1, "3202", 1, "被套期项目", 148, null, 1 },
                    { 149, 3, "4001", 2, "实收资本", 149, null, 1 },
                    { 150, 3, "4002", 2, "资本公积", 150, null, 1 },
                    { 151, 3, "4003", 2, "其他权益工具", 151, null, 1 },
                    { 152, 3, "4003001", 2, "优先股", 152, "4003", 1 },
                    { 153, 3, "4003002", 2, "永续债", 153, "4003", 1 },
                    { 154, 3, "4004", 2, "其他综合收益", 154, null, 1 },
                    { 155, 3, "4101", 2, "盈余公积", 155, null, 1 },
                    { 156, 3, "4101001", 2, "法定盈余公积", 156, "4101", 1 },
                    { 157, 3, "4101002", 2, "任意盈余公积", 157, "4101", 1 },
                    { 158, 3, "4101003", 2, "法定公益金", 158, "4101", 1 },
                    { 159, 3, "4102", 2, "一般风险准备", 159, null, 1 },
                    { 160, 3, "4103", 2, "本年利润", 160, null, 1 },
                    { 161, 3, "4104", 2, "利润分配", 161, null, 1 },
                    { 162, 3, "4104001", 2, "其他转入", 162, "4104", 1 },
                    { 163, 3, "4104002", 2, "提取法定盈余公积", 163, "4104", 1 },
                    { 164, 3, "4104003", 2, "提取法定公益金", 164, "4104", 1 },
                    { 165, 3, "4104004", 2, "提取任意盈余公积", 165, "4104", 1 },
                    { 166, 3, "4104005", 2, "应付利润", 166, "4104", 1 },
                    { 167, 3, "4104006", 2, "未分配利润", 167, "4104", 1 },
                    { 168, 3, "4105", 2, "专项储备", 168, null, 1 },
                    { 169, 3, "4201", 1, "库存股", 169, null, 1 },
                    { 170, 4, "5001", 1, "生产成本", 170, null, 1 },
                    { 171, 4, "5101", 1, "制造费用", 171, null, 1 },
                    { 172, 4, "5201", 1, "劳务成本", 172, null, 1 },
                    { 173, 4, "5301", 1, "研发支出", 173, null, 1 },
                    { 174, 4, "5401", 1, "工程施工", 174, null, 1 },
                    { 175, 4, "5402", 2, "工程结算", 175, null, 1 },
                    { 176, 4, "5403", 1, "机械作业", 176, null, 1 },
                    { 177, 5, "6001", 2, "主营业务收入", 177, null, 1 },
                    { 178, 5, "6011", 2, "利息收入", 178, null, 1 },
                    { 179, 5, "6021", 2, "手续费及佣金收入", 179, null, 1 },
                    { 180, 5, "6031", 2, "保费收入", 180, null, 1 },
                    { 181, 5, "6041", 2, "租赁收入", 181, null, 1 },
                    { 182, 5, "6051", 2, "其他业务收入", 182, null, 1 },
                    { 183, 5, "6061", 2, "汇兑损益", 183, null, 1 },
                    { 184, 5, "6101", 2, "公允价值变动损益", 184, null, 1 },
                    { 185, 5, "6111", 2, "投资收益", 185, null, 1 },
                    { 186, 5, "6201", 2, "摊回保险责任准备金", 186, null, 1 },
                    { 187, 5, "6202", 2, "摊回赔付支出", 187, null, 1 },
                    { 188, 5, "6203", 2, "摊回分保费用", 188, null, 1 },
                    { 189, 5, "6301", 2, "营业外收入", 189, null, 1 },
                    { 190, 5, "6301001", 2, "非流动资产处置净收益", 190, "6301", 1 },
                    { 191, 5, "6301002", 2, "政府补助", 191, "6301", 1 },
                    { 192, 5, "6301003", 2, "捐赠收益", 192, "6301", 1 },
                    { 193, 5, "6301004", 2, "盘盈收益", 193, "6301", 1 },
                    { 194, 5, "6401", 1, "主营业务成本", 194, null, 1 },
                    { 195, 5, "6402", 1, "其他业务成本", 195, null, 1 },
                    { 196, 5, "6403", 1, "税金及附加", 196, null, 1 },
                    { 197, 5, "6411", 1, "利息支出", 197, null, 1 },
                    { 198, 5, "6421", 1, "手续费及佣金支出", 198, null, 1 },
                    { 199, 5, "6501", 1, "提取未到期责任准备金", 199, null, 1 },
                    { 200, 5, "6502", 1, "提取保险责任准备金", 200, null, 1 },
                    { 201, 5, "6511", 1, "赔付支出", 201, null, 1 },
                    { 202, 5, "6521", 1, "保单红利支出", 202, null, 1 },
                    { 203, 5, "6531", 1, "退保金", 203, null, 1 },
                    { 204, 5, "6541", 1, "分出保费", 204, null, 1 },
                    { 205, 5, "6542", 1, "分保费用", 205, null, 1 },
                    { 206, 5, "6601", 1, "销售费用", 206, null, 1 },
                    { 207, 5, "6601001", 1, "销售人员职工薪酬", 207, "6601", 1 },
                    { 208, 5, "6601002", 1, "业务招待费", 208, "6601", 1 },
                    { 209, 5, "6601003", 1, "修理费", 209, "6601", 1 },
                    { 210, 5, "6601004", 1, "办公费", 210, "6601", 1 },
                    { 211, 5, "6601005", 1, "水电费", 211, "6601", 1 },
                    { 212, 5, "6601006", 1, "差旅费", 212, "6601", 1 },
                    { 213, 5, "6601007", 1, "折旧费", 213, "6601", 1 },
                    { 214, 5, "6601008", 1, "摊销费", 214, "6601", 1 },
                    { 215, 5, "6601009", 1, "展览费", 215, "6601", 1 },
                    { 216, 5, "6601010", 1, "商品维修费", 216, "6601", 1 },
                    { 217, 5, "6601011", 1, "运输费", 217, "6601", 1 },
                    { 218, 5, "6601012", 1, "装卸费", 218, "6601", 1 },
                    { 219, 5, "6601013", 1, "包装费", 219, "6601", 1 },
                    { 220, 5, "6601014", 1, "保险费", 220, "6601", 1 },
                    { 221, 5, "6601015", 1, "广告费", 221, "6601", 1 },
                    { 222, 5, "6601016", 1, "业务宣传费", 222, "6601", 1 },
                    { 223, 5, "6602", 1, "管理费用", 223, null, 1 },
                    { 224, 5, "6602001", 1, "管理人员职工薪酬", 224, "6602", 1 },
                    { 225, 5, "6602002", 1, "业务招待费", 225, "6602", 1 },
                    { 226, 5, "6602003", 1, "修理费", 226, "6602", 1 },
                    { 227, 5, "6602004", 1, "办公费", 227, "6602", 1 },
                    { 228, 5, "6602005", 1, "水电费", 228, "6602", 1 },
                    { 229, 5, "6602006", 1, "差旅费", 229, "6602", 1 },
                    { 230, 5, "6602007", 1, "折旧费", 230, "6602", 1 },
                    { 231, 5, "6602008", 1, "摊销费", 231, "6602", 1 },
                    { 232, 5, "6602009", 1, "开办费", 232, "6602", 1 },
                    { 233, 5, "6602010", 1, "研究费用", 233, "6602", 1 },
                    { 234, 5, "6602011", 1, "咨询费", 234, "6602", 1 },
                    { 235, 5, "6602012", 1, "长期待摊费用摊销", 235, "6602", 1 },
                    { 236, 5, "6602013", 1, "租赁费", 236, "6602", 1 },
                    { 237, 5, "6603", 1, "财务费用", 237, null, 1 },
                    { 238, 5, "6603001", 1, "利息费用", 238, "6603", 1 },
                    { 239, 5, "6603002", 1, "手续费", 239, "6603", 1 },
                    { 240, 5, "6603003", 1, "汇兑损益", 240, "6603", 1 },
                    { 241, 5, "6603004", 1, "现金折扣", 241, "6603", 1 },
                    { 242, 5, "6604", 1, "勘探费用", 242, null, 1 },
                    { 243, 5, "6701", 1, "资产减值损失", 243, null, 1 },
                    { 244, 5, "6711", 1, "营业外支出", 244, null, 1 },
                    { 245, 5, "6711001", 1, "非流动资产处置净损失", 245, "6711", 1 },
                    { 246, 5, "6711002", 1, "赞助支出", 246, "6711", 1 },
                    { 247, 5, "6711003", 1, "捐赠支出", 247, "6711", 1 },
                    { 248, 5, "6711004", 1, "盘亏损失", 248, "6711", 1 },
                    { 249, 5, "6711005", 1, "坏账损失", 249, "6711", 1 },
                    { 250, 5, "6711006", 1, "存货毁损报废损失", 250, "6711", 1 },
                    { 251, 5, "6711007", 1, "无法收回的长期债券投资损失", 251, "6711", 1 },
                    { 252, 5, "6711008", 1, "无法收回的长期股权投资损失", 252, "6711", 1 },
                    { 253, 5, "6711009", 1, "自然灾害等不可抗力因素造成的损失", 253, "6711", 1 },
                    { 254, 5, "6711010", 1, "税收滞纳金", 254, "6711", 1 },
                    { 255, 5, "6711011", 1, "罚款支出", 255, "6711", 1 },
                    { 256, 5, "6801", 1, "所得税费用", 256, null, 1 },
                    { 257, 5, "6901", 1, "以前年度损益调整", 257, null, 1 },
                    { 258, 1, "1001", 1, "库存现金", 1, null, 2 },
                    { 259, 1, "1002", 1, "银行存款", 2, null, 2 },
                    { 260, 1, "1012", 1, "其他货币资金", 3, null, 2 },
                    { 261, 1, "1101", 1, "短期投资", 4, null, 2 },
                    { 262, 1, "110101", 1, "股票", 5, "1101", 2 },
                    { 263, 1, "110102", 1, "债券", 6, "1101", 2 },
                    { 264, 1, "110103", 1, "基金", 7, "1101", 2 },
                    { 265, 1, "110110", 1, "其他", 8, "1101", 2 },
                    { 266, 1, "1121", 1, "应收票据", 9, null, 2 },
                    { 267, 1, "1122", 1, "应收账款", 10, null, 2 },
                    { 268, 1, "1123", 1, "预付账款", 11, null, 2 },
                    { 269, 1, "1131", 1, "应收股利", 12, null, 2 },
                    { 270, 1, "1132", 1, "应收利息", 13, null, 2 },
                    { 271, 1, "1221", 1, "其他应收款", 14, null, 2 },
                    { 272, 1, "1401", 1, "材料采购", 15, null, 2 },
                    { 273, 1, "1402", 1, "在途物资", 16, null, 2 },
                    { 274, 1, "1403", 1, "原材料", 17, null, 2 },
                    { 275, 1, "1404", 1, "材料成本差异", 18, null, 2 },
                    { 276, 1, "1405", 1, "库存商品", 19, null, 2 },
                    { 277, 1, "1407", 2, "商品进销差价", 20, null, 2 },
                    { 278, 1, "1408", 1, "委托加工物资", 21, null, 2 },
                    { 279, 1, "1411", 1, "周转材料", 22, null, 2 },
                    { 280, 1, "1421", 1, "消耗性生物资产", 23, null, 2 },
                    { 281, 1, "1501", 1, "长期债券投资", 24, null, 2 },
                    { 282, 1, "150101", 1, "债券投资", 25, "1501", 2 },
                    { 283, 1, "150102", 1, "其他债权投资", 26, "1501", 2 },
                    { 284, 1, "1511", 1, "长期股权投资", 27, null, 2 },
                    { 285, 1, "151101", 1, "股票投资", 28, "1511", 2 },
                    { 286, 1, "151102", 1, "其他股权投资", 29, "1511", 2 },
                    { 287, 1, "1601", 1, "固定资产", 30, null, 2 },
                    { 288, 1, "1602", 2, "累计折旧", 31, null, 2 },
                    { 289, 1, "1604", 1, "在建工程", 32, null, 2 },
                    { 290, 1, "160401", 1, "建筑工程", 33, "1604", 2 },
                    { 291, 1, "160402", 1, "安装工程", 34, "1604", 2 },
                    { 292, 1, "160403", 1, "技术改造工程", 35, "1604", 2 },
                    { 293, 1, "160404", 1, "其他支出", 36, "1604", 2 },
                    { 294, 1, "1605", 1, "工程物资", 37, null, 2 },
                    { 295, 1, "1606", 1, "固定资产清理", 38, null, 2 },
                    { 296, 1, "1621", 1, "生产性生物资产", 39, null, 2 },
                    { 297, 1, "1622", 2, "生产性生物资产累计折旧", 40, null, 2 },
                    { 298, 1, "1701", 1, "无形资产", 41, null, 2 },
                    { 299, 1, "1702", 2, "累计摊销", 42, null, 2 },
                    { 300, 1, "1801", 1, "长期待摊费用", 43, null, 2 },
                    { 301, 1, "1901", 1, "待处理财产损溢", 44, null, 2 },
                    { 302, 2, "2001", 2, "短期借款", 45, null, 2 },
                    { 303, 2, "2201", 2, "应付票据", 46, null, 2 },
                    { 304, 2, "2202", 2, "应付账款", 47, null, 2 },
                    { 305, 2, "2203", 2, "预收账款", 48, null, 2 },
                    { 306, 2, "2211", 2, "应付职工薪酬", 49, null, 2 },
                    { 307, 2, "221101", 2, "工资", 50, "2211", 2 },
                    { 308, 2, "221102", 2, "奖金", 51, "2211", 2 },
                    { 309, 2, "2221", 2, "应交税费", 52, null, 2 },
                    { 310, 2, "222101", 2, "应交增值税", 53, "2221", 2 },
                    { 311, 2, "22210101", 1, "进项税额", 54, "222101", 2 },
                    { 312, 2, "22210102", 1, "销项税额的抵减", 55, "222101", 2 },
                    { 313, 2, "22210103", 1, "已交税金", 56, "222101", 2 },
                    { 314, 2, "22210104", 1, "转出未交增值税", 57, "222101", 2 },
                    { 315, 2, "22210105", 1, "减免税款", 58, "222101", 2 },
                    { 316, 2, "22210106", 1, "出口抵减内销产品应纳税额", 59, "222101", 2 },
                    { 317, 2, "22210107", 2, "销项税额", 60, "222101", 2 },
                    { 318, 2, "22210108", 2, "出口退税", 61, "222101", 2 },
                    { 319, 2, "22210109", 2, "进项税额转出", 62, "222101", 2 },
                    { 320, 2, "22210110", 2, "转出多交增值税", 63, "222101", 2 },
                    { 321, 2, "222102", 2, "未交增值税", 64, "2221", 2 },
                    { 322, 2, "222103", 1, "预交增值税", 65, "2221", 2 },
                    { 323, 2, "222104", 1, "待抵扣进项税额", 66, "2221", 2 },
                    { 324, 2, "222105", 1, "待认证进项税额", 67, "2221", 2 },
                    { 325, 2, "222106", 2, "待转销项税额", 68, "2221", 2 },
                    { 326, 2, "222107", 1, "增值税留抵税额", 69, "2221", 2 },
                    { 327, 2, "222108", 2, "简易计税", 70, "2221", 2 },
                    { 328, 2, "222109", 2, "转让金融商品应交增值税", 71, "2221", 2 },
                    { 329, 2, "222110", 2, "代扣代交增值税", 72, "2221", 2 },
                    { 330, 2, "222111", 2, "应交所得税", 73, "2221", 2 },
                    { 331, 2, "222112", 2, "应交个人所得税", 74, "2221", 2 },
                    { 332, 2, "222113", 2, "教育费附加", 75, "2221", 2 },
                    { 333, 2, "222114", 2, "地方教育费附加", 76, "2221", 2 },
                    { 334, 2, "222115", 2, "应交资源税", 77, "2221", 2 },
                    { 335, 2, "222116", 2, "应交土地增值税", 78, "2221", 2 },
                    { 336, 2, "222117", 2, "应交城市维护建设税", 79, "2221", 2 },
                    { 337, 2, "222118", 2, "应交房产税", 80, "2221", 2 },
                    { 338, 2, "222119", 2, "应交土地使用税", 81, "2221", 2 },
                    { 339, 2, "222120", 2, "应交车船使用税", 82, "2221", 2 },
                    { 340, 2, "222121", 2, "应交消费税", 83, "2221", 2 },
                    { 341, 2, "222122", 2, "应交印花税", 84, "2221", 2 },
                    { 342, 2, "222123", 2, "应交关税", 85, "2221", 2 },
                    { 343, 2, "2231", 2, "应付利息", 86, null, 2 },
                    { 344, 2, "2232", 2, "应付利润", 87, null, 2 },
                    { 345, 2, "2241", 2, "其他应付款", 88, null, 2 },
                    { 346, 2, "2401", 2, "递延收益", 89, null, 2 },
                    { 347, 2, "2501", 2, "长期借款", 90, null, 2 },
                    { 348, 2, "2701", 2, "长期应付款", 91, null, 2 },
                    { 349, 3, "3001", 2, "实收资本", 92, null, 2 },
                    { 350, 3, "3002", 2, "资本公积", 93, null, 2 },
                    { 351, 3, "300201", 2, "资本溢价", 94, "3002", 2 },
                    { 352, 3, "300202", 2, "接受捐赠非现金资产准备", 95, "3002", 2 },
                    { 353, 3, "300206", 2, "外币资本折算差额", 96, "3002", 2 },
                    { 354, 3, "300207", 2, "其他资本公积", 97, "3002", 2 },
                    { 355, 3, "3101", 2, "盈余公积", 98, null, 2 },
                    { 356, 3, "310101", 2, "法定盈余公积", 99, "3101", 2 },
                    { 357, 3, "310102", 2, "任意盈余公积", 100, "3101", 2 },
                    { 358, 3, "310103", 2, "法定公益金", 101, "3101", 2 },
                    { 359, 3, "3103", 2, "本年利润", 102, null, 2 },
                    { 360, 3, "3104", 2, "利润分配", 103, null, 2 },
                    { 361, 3, "310401", 1, "其他转入", 104, "3104", 2 },
                    { 362, 3, "310402", 1, "提取法定盈余公积", 105, "3104", 2 },
                    { 363, 3, "310403", 1, "提取法定公益金", 106, "3104", 2 },
                    { 364, 3, "310409", 1, "提取任意盈余公积", 107, "3104", 2 },
                    { 365, 3, "310410", 1, "应付利润", 108, "3104", 2 },
                    { 366, 3, "310411", 1, "转作资本的利润", 109, "3104", 2 },
                    { 367, 3, "310415", 2, "未分配利润", 110, "3104", 2 },
                    { 368, 4, "4001", 1, "生产成本", 111, null, 2 },
                    { 369, 4, "400101", 1, "基本生产成本", 112, "4001", 2 },
                    { 370, 4, "400102", 1, "辅助生产成本", 113, "4001", 2 },
                    { 371, 4, "4101", 1, "制造费用", 114, null, 2 },
                    { 372, 4, "4301", 1, "研发支出", 115, null, 2 },
                    { 373, 4, "4401", 1, "工程施工", 116, null, 2 },
                    { 374, 4, "4403", 1, "机械作业", 117, null, 2 },
                    { 375, 5, "5001", 2, "主营业务收入", 118, null, 2 },
                    { 376, 5, "5051", 2, "其他业务收入", 119, null, 2 },
                    { 377, 5, "5111", 2, "投资收益", 120, null, 2 },
                    { 378, 5, "5301", 2, "营业外收入", 121, null, 2 },
                    { 379, 5, "530101", 2, "非流动资产处置净收益", 122, "5301", 2 },
                    { 380, 5, "530102", 2, "政府补助", 123, "5301", 2 },
                    { 381, 5, "530103", 2, "捐赠收益", 124, "5301", 2 },
                    { 382, 5, "530104", 2, "盘盈收益", 125, "5301", 2 },
                    { 383, 5, "530105", 2, "其他", 126, "5301", 2 },
                    { 384, 5, "5401", 1, "主营业务成本", 127, null, 2 },
                    { 385, 5, "5402", 1, "其他业务成本", 128, null, 2 },
                    { 386, 5, "5403", 1, "税金及附加", 129, null, 2 },
                    { 387, 5, "540301", 1, "消费税", 130, "5403", 2 },
                    { 388, 5, "540303", 1, "城市维护建设税", 131, "5403", 2 },
                    { 389, 5, "540304", 1, "资源税", 132, "5403", 2 },
                    { 390, 5, "540305", 1, "土地增值税", 133, "5403", 2 },
                    { 391, 5, "540306", 1, "城镇土地使用税", 134, "5403", 2 },
                    { 392, 5, "540307", 1, "房产税", 135, "5403", 2 },
                    { 393, 5, "540308", 1, "车船税", 136, "5403", 2 },
                    { 394, 5, "540309", 1, "印花税", 137, "5403", 2 },
                    { 395, 5, "540310", 1, "教育费附加", 138, "5403", 2 },
                    { 396, 5, "540311", 1, "矿产资源补偿费", 139, "5403", 2 },
                    { 397, 5, "540312", 1, "排污费", 140, "5403", 2 },
                    { 398, 5, "540313", 1, "地方教育费附加", 141, "5403", 2 },
                    { 399, 5, "540399", 1, "其他", 142, "5403", 2 },
                    { 400, 5, "5601", 1, "销售费用", 143, null, 2 },
                    { 401, 5, "560101", 1, "办公费", 144, "5601", 2 },
                    { 402, 5, "560102", 1, "房租", 145, "5601", 2 },
                    { 403, 5, "560103", 1, "物业管理费", 146, "5601", 2 },
                    { 404, 5, "560104", 1, "水电费", 147, "5601", 2 },
                    { 405, 5, "560105", 1, "交际应酬费", 148, "5601", 2 },
                    { 406, 5, "560106", 1, "市内交通费", 149, "5601", 2 },
                    { 407, 5, "560107", 1, "差旅费", 150, "5601", 2 },
                    { 408, 5, "560108", 1, "补助", 151, "5601", 2 },
                    { 409, 5, "560109", 1, "通讯费", 152, "5601", 2 },
                    { 410, 5, "560110", 1, "工资", 153, "5601", 2 },
                    { 411, 5, "560111", 1, "佣金", 154, "5601", 2 },
                    { 412, 5, "560112", 1, "保险金", 155, "5601", 2 },
                    { 413, 5, "560113", 1, "福利费", 156, "5601", 2 },
                    { 414, 5, "560114", 1, "累计折旧", 157, "5601", 2 },
                    { 415, 5, "560115", 1, "商品维修费", 158, "5601", 2 },
                    { 416, 5, "560116", 1, "广告和业务宣传费", 159, "5601", 2 },
                    { 417, 5, "560117", 1, "业务招待费", 160, "5601", 2 },
                    { 418, 5, "560118", 1, "劳务费", 161, "5601", 2 },
                    { 419, 5, "560199", 1, "其他", 162, "5601", 2 },
                    { 420, 5, "5602", 1, "管理费用", 163, null, 2 },
                    { 421, 5, "560201", 1, "办公费", 164, "5602", 2 },
                    { 422, 5, "560202", 1, "房租", 165, "5602", 2 },
                    { 423, 5, "560203", 1, "物业管理费", 166, "5602", 2 },
                    { 424, 5, "560204", 1, "水电费", 167, "5602", 2 },
                    { 425, 5, "560205", 1, "交际应酬费", 168, "5602", 2 },
                    { 426, 5, "560206", 1, "市内交通费", 169, "5602", 2 },
                    { 427, 5, "560207", 1, "差旅费", 170, "5602", 2 },
                    { 428, 5, "560208", 1, "通讯费", 171, "5602", 2 },
                    { 429, 5, "560209", 1, "工资", 172, "5602", 2 },
                    { 430, 5, "560210", 1, "保险金", 173, "5602", 2 },
                    { 431, 5, "560211", 1, "福利费", 174, "5602", 2 },
                    { 432, 5, "560212", 1, "折旧费", 175, "5602", 2 },
                    { 433, 5, "560213", 1, "开办费", 176, "5602", 2 },
                    { 434, 5, "560214", 1, "职工教育经费", 177, "5602", 2 },
                    { 435, 5, "560215", 1, "研究费用", 178, "5602", 2 },
                    { 436, 5, "560216", 1, "交通费", 179, "5602", 2 },
                    { 437, 5, "560217", 1, "运杂费", 180, "5602", 2 },
                    { 438, 5, "560218", 1, "修理费", 181, "5602", 2 },
                    { 439, 5, "560219", 1, "保险费", 182, "5602", 2 },
                    { 440, 5, "560220", 1, "公积金", 183, "5602", 2 },
                    { 441, 5, "560221", 1, "社保", 184, "5602", 2 },
                    { 442, 5, "560222", 1, "汽车费用", 185, "5602", 2 },
                    { 443, 5, "560223", 1, "服务费", 186, "5602", 2 },
                    { 444, 5, "560224", 1, "咨询费", 187, "5602", 2 },
                    { 445, 5, "560225", 1, "无形资产摊销", 188, "5602", 2 },
                    { 446, 5, "560226", 1, "招聘费", 189, "5602", 2 },
                    { 447, 5, "560227", 1, "业务招待费", 190, "5602", 2 },
                    { 448, 5, "560299", 1, "其他", 191, "5602", 2 },
                    { 449, 5, "5603", 1, "财务费用", 192, null, 2 },
                    { 450, 5, "560301", 1, "汇兑损益", 193, "5603", 2 },
                    { 451, 5, "560302", 1, "利息", 194, "5603", 2 },
                    { 452, 5, "560303", 1, "手续费", 195, "5603", 2 },
                    { 453, 5, "560304", 1, "账户管理费", 196, "5603", 2 },
                    { 454, 5, "560399", 1, "其他", 197, "5603", 2 },
                    { 455, 5, "5711", 1, "营业外支出", 198, null, 2 },
                    { 456, 5, "571101", 1, "存货盘亏毁损", 199, "5711", 2 },
                    { 457, 5, "571102", 1, "非流动资产处置净损失", 200, "5711", 2 },
                    { 458, 5, "571103", 1, "坏账损失", 201, "5711", 2 },
                    { 459, 5, "571104", 1, "无法收回的长期债券投资损失", 202, "5711", 2 },
                    { 460, 5, "571105", 1, "无法收回的长期股权投资损失", 203, "5711", 2 },
                    { 461, 5, "571106", 1, "自然灾害等不可抗力造成的损失", 204, "5711", 2 },
                    { 462, 5, "571107", 1, "税收滞纳金", 205, "5711", 2 },
                    { 463, 5, "571108", 1, "罚金、罚款", 206, "5711", 2 },
                    { 464, 5, "571109", 1, "捐赠支出", 207, "5711", 2 },
                    { 465, 5, "571110", 1, "其他", 208, "5711", 2 },
                    { 466, 5, "5801", 1, "所得税费用", 209, null, 2 },
                    { 467, 5, "6000", 1, "以前年度损益调整", 210, null, 2 },
                    { 468, 1, "1001", 1, "库存现金", 1, null, 4 },
                    { 469, 1, "1002", 1, "银行存款", 2, null, 4 },
                    { 470, 1, "1012", 1, "其他货币资金", 3, null, 4 },
                    { 471, 1, "101201", 1, "外埠存款", 4, "1012", 4 },
                    { 472, 1, "101202", 1, "银行本票存款", 5, "1012", 4 },
                    { 473, 1, "101203", 1, "银行汇票存款", 6, "1012", 4 },
                    { 474, 1, "101204", 1, "信用卡存款", 7, "1012", 4 },
                    { 475, 1, "101205", 1, "信用证保证金存款", 8, "1012", 4 },
                    { 476, 1, "101206", 1, "存出投资款", 9, "1012", 4 },
                    { 477, 1, "1101", 1, "交易性金融资产", 10, null, 4 },
                    { 478, 1, "110101", 1, "本金", 11, "1101", 4 },
                    { 479, 1, "11010101", 1, "股票", 12, "110101", 4 },
                    { 480, 1, "11010102", 1, "债券", 13, "110101", 4 },
                    { 481, 1, "11010103", 1, "基金", 14, "110101", 4 },
                    { 482, 1, "11010104", 1, "权证", 15, "110101", 4 },
                    { 483, 1, "11010199", 1, "其他", 16, "110101", 4 },
                    { 484, 1, "110102", 1, "公允价值变动", 17, "1101", 4 },
                    { 485, 1, "11010201", 1, "股票", 18, "110102", 4 },
                    { 486, 1, "11010202", 1, "债券", 19, "110102", 4 },
                    { 487, 1, "11010203", 1, "基金", 20, "110102", 4 },
                    { 488, 1, "11010204", 1, "权证", 21, "110102", 4 },
                    { 489, 1, "11010299", 1, "其他", 22, "110102", 4 },
                    { 490, 1, "1121", 1, "应收票据", 23, null, 4 },
                    { 491, 1, "1122", 1, "应收账款", 24, null, 4 },
                    { 492, 1, "1123", 1, "预付账款", 25, null, 4 },
                    { 493, 1, "1131", 1, "应收股利", 26, null, 4 },
                    { 494, 1, "1132", 1, "应收利息", 27, null, 4 },
                    { 495, 1, "1221", 1, "其他应收款", 28, null, 4 },
                    { 496, 1, "1231", 2, "坏账准备", 29, null, 4 },
                    { 497, 1, "123101", 2, "应收账款坏账准备", 30, "1231", 4 },
                    { 498, 1, "123102", 2, "其他应收款坏账准备", 31, "1231", 4 },
                    { 499, 1, "1321", 1, "受托代销商品", 32, null, 4 },
                    { 500, 1, "1401", 1, "材料采购", 33, null, 4 },
                    { 501, 1, "1402", 1, "在途物资", 34, null, 4 },
                    { 502, 1, "1403", 1, "原材料", 35, null, 4 },
                    { 503, 1, "1404", 1, "材料成本差异", 36, null, 4 },
                    { 504, 1, "1405", 1, "库存商品", 37, null, 4 },
                    { 505, 1, "1406", 1, "发出商品", 38, null, 4 },
                    { 506, 1, "1407", 2, "商品进销差价", 39, null, 4 },
                    { 507, 1, "1408", 1, "委托加工物资", 40, null, 4 },
                    { 508, 1, "1411", 1, "周转材料", 41, null, 4 },
                    { 509, 1, "1471", 2, "存货跌价准备", 42, null, 4 },
                    { 510, 1, "1501", 1, "持有至到期投资", 43, null, 4 },
                    { 511, 1, "150101", 1, "投资成本", 44, "1501", 4 },
                    { 512, 1, "150102", 1, "溢折价", 45, "1501", 4 },
                    { 513, 1, "150103", 1, "应计利息", 46, "1501", 4 },
                    { 514, 1, "1502", 2, "持有至到期投资减值准备", 47, null, 4 },
                    { 515, 1, "1503", 1, "可供出售金融资产", 48, null, 4 },
                    { 516, 1, "1511", 1, "长期股权投资", 49, null, 4 },
                    { 517, 1, "151101", 1, "投资成本", 50, "1511", 4 },
                    { 518, 1, "151102", 1, "损益调整", 51, "1511", 4 },
                    { 519, 1, "151103", 1, "所有者权益其他变动", 52, "1511", 4 },
                    { 520, 1, "1512", 2, "长期股权投资减值准备", 53, null, 4 },
                    { 521, 1, "1521", 1, "投资性房地产", 54, null, 4 },
                    { 522, 1, "152101", 1, "成本", 55, "1521", 4 },
                    { 523, 1, "152102", 1, "公允价值变动", 56, "1521", 4 },
                    { 524, 1, "1531", 1, "长期应收款", 57, null, 4 },
                    { 525, 1, "1532", 1, "未实现融资收益", 58, null, 4 },
                    { 526, 1, "1601", 1, "固定资产", 59, null, 4 },
                    { 527, 1, "1602", 2, "累计折旧", 60, null, 4 },
                    { 528, 1, "1603", 2, "固定资产减值准备", 61, null, 4 },
                    { 529, 1, "1604", 1, "在建工程", 62, null, 4 },
                    { 530, 1, "160401", 1, "建筑工程", 63, "1604", 4 },
                    { 531, 1, "160402", 1, "安装工程", 64, "1604", 4 },
                    { 532, 1, "160403", 1, "在安装设备", 65, "1604", 4 },
                    { 533, 1, "160404", 1, "待摊支出", 66, "1604", 4 },
                    { 534, 1, "1605", 1, "工程物资", 67, null, 4 },
                    { 535, 1, "160501", 1, "专用材料", 68, "1605", 4 },
                    { 536, 1, "160502", 1, "专用设备", 69, "1605", 4 },
                    { 537, 1, "160503", 1, "预付大型设备款", 70, "1605", 4 },
                    { 538, 1, "160504", 1, "为生产准备的工具及器具", 71, "1605", 4 },
                    { 539, 1, "1606", 1, "固定资产清理", 72, null, 4 },
                    { 540, 1, "1621", 1, "生产性生物资产", 73, null, 4 },
                    { 541, 1, "1622", 2, "生产性生物资产累计折旧", 74, null, 4 },
                    { 542, 1, "1701", 1, "无形资产", 75, null, 4 },
                    { 543, 1, "1702", 2, "累计摊销", 76, null, 4 },
                    { 544, 1, "1703", 2, "无形资产减值准备", 77, null, 4 },
                    { 545, 1, "1711", 1, "商誉", 78, null, 4 },
                    { 546, 1, "1801", 1, "长期待摊费用", 79, null, 4 },
                    { 547, 1, "1811", 1, "递延所得税资产", 80, null, 4 },
                    { 548, 1, "1901", 1, "待处理财产损溢", 81, null, 4 },
                    { 549, 2, "2001", 2, "短期借款", 82, null, 4 },
                    { 550, 2, "2101", 2, "交易性金融负债", 83, null, 4 },
                    { 551, 2, "210101", 2, "本金", 84, "2101", 4 },
                    { 552, 2, "210102", 2, "公允价值变动", 85, "2101", 4 },
                    { 553, 2, "2201", 2, "应付票据", 86, null, 4 },
                    { 554, 2, "2202", 2, "应付账款", 87, null, 4 },
                    { 555, 2, "2203", 2, "预收账款", 88, null, 4 },
                    { 556, 2, "2211", 2, "应付职工薪酬", 89, null, 4 },
                    { 557, 2, "221101", 2, "工资", 90, "2211", 4 },
                    { 558, 2, "221102", 2, "职工福利", 91, "2211", 4 },
                    { 559, 2, "221103", 2, "社会保险费", 92, "2211", 4 },
                    { 560, 2, "221104", 2, "住房公积金", 93, "2211", 4 },
                    { 561, 2, "221105", 2, "工会经费", 94, "2211", 4 },
                    { 562, 2, "221106", 2, "职工教育经费", 95, "2211", 4 },
                    { 563, 2, "221107", 2, "解除职工劳动关系补偿", 96, "2211", 4 },
                    { 564, 2, "221108", 2, "奖金", 97, "2211", 4 },
                    { 565, 2, "2221", 2, "应交税费", 98, null, 4 },
                    { 566, 2, "222101", 2, "应交增值税", 99, "2221", 4 },
                    { 567, 2, "22210101", 1, "进项税额", 100, "222101", 4 },
                    { 568, 2, "22210102", 1, "销项税额抵减", 101, "222101", 4 },
                    { 569, 2, "22210103", 1, "已交税金", 102, "222101", 4 },
                    { 570, 2, "22210104", 1, "转出未交增值税", 103, "222101", 4 },
                    { 571, 2, "22210105", 1, "减免税款", 104, "222101", 4 },
                    { 572, 2, "22210106", 1, "出口抵减内销产品应纳税额", 105, "222101", 4 },
                    { 573, 2, "22210107", 2, "销项税额", 106, "222101", 4 },
                    { 574, 2, "22210108", 2, "出口退税", 107, "222101", 4 },
                    { 575, 2, "22210109", 2, "进项税额转出", 108, "222101", 4 },
                    { 576, 2, "22210110", 2, "转出多交增值税", 109, "222101", 4 },
                    { 577, 2, "222102", 2, "未交增值税", 110, "2221", 4 },
                    { 578, 2, "222103", 1, "预交增值税", 111, "2221", 4 },
                    { 579, 2, "222104", 1, "待抵扣进项税额", 112, "2221", 4 },
                    { 580, 2, "222105", 1, "待认证进项税额", 113, "2221", 4 },
                    { 581, 2, "222106", 2, "待转销项税额", 114, "2221", 4 },
                    { 582, 2, "222107", 1, "增值税留抵税额", 115, "2221", 4 },
                    { 583, 2, "222108", 2, "简易计税", 116, "2221", 4 },
                    { 584, 2, "222109", 2, "转让金融商品应交增值税", 117, "2221", 4 },
                    { 585, 2, "222110", 2, "代扣代交增值税", 118, "2221", 4 },
                    { 586, 2, "222111", 2, "应交所得税", 119, "2221", 4 },
                    { 587, 2, "222112", 2, "应交个人所得税", 120, "2221", 4 },
                    { 588, 2, "222113", 2, "应交教育费附加", 121, "2221", 4 },
                    { 589, 2, "222114", 2, "应交城市维护建设税", 122, "2221", 4 },
                    { 590, 2, "222115", 2, "应交地方教育费附加", 123, "2221", 4 },
                    { 591, 2, "222116", 2, "应交土地增值税", 124, "2221", 4 },
                    { 592, 2, "222117", 2, "应交资源税", 125, "2221", 4 },
                    { 593, 2, "222118", 2, "应交房产税", 126, "2221", 4 },
                    { 594, 2, "222119", 2, "应交土地使用税", 127, "2221", 4 },
                    { 595, 2, "222120", 2, "应交车船使用税", 128, "2221", 4 },
                    { 596, 2, "222121", 2, "应交消费税", 129, "2221", 4 },
                    { 597, 2, "222122", 2, "应交矿产资源补偿费", 130, "2221", 4 },
                    { 598, 2, "222123", 2, "应交印花税", 131, "2221", 4 },
                    { 599, 2, "222124", 2, "应交关税", 132, "2221", 4 },
                    { 600, 2, "2231", 2, "应付股利", 133, null, 4 },
                    { 601, 2, "2232", 2, "应付利息", 134, null, 4 },
                    { 602, 2, "2241", 2, "其他应付款", 135, null, 4 },
                    { 603, 2, "2314", 2, "受托代湮商品款", 136, null, 4 },
                    { 604, 2, "2401", 2, "递延收益", 137, null, 4 },
                    { 605, 2, "2501", 2, "长期借款", 138, null, 4 },
                    { 606, 2, "250101", 2, "本金", 139, "2501", 4 },
                    { 607, 2, "250102", 2, "利息调整", 140, "2501", 4 },
                    { 608, 2, "2502", 2, "应付债券", 141, null, 4 },
                    { 609, 2, "250201", 2, "债券面值", 142, "2502", 4 },
                    { 610, 2, "250202", 2, "利息调整", 143, "2502", 4 },
                    { 611, 2, "250203", 2, "应计利息", 144, "2502", 4 },
                    { 612, 2, "2701", 2, "长期应付款", 145, null, 4 },
                    { 613, 2, "2702", 2, "未确认融资费用", 146, null, 4 },
                    { 614, 2, "2711", 2, "专项应付款", 147, null, 4 },
                    { 615, 2, "2801", 2, "预计负债", 148, null, 4 },
                    { 616, 2, "2901", 2, "递延所得税负债", 149, null, 4 },
                    { 617, 1, "3101", 1, "衍生工具", 150, null, 4 },
                    { 618, 1, "3201", 1, "套期工具", 151, null, 4 },
                    { 619, 1, "3202", 1, "被套期项目", 152, null, 4 },
                    { 620, 3, "4001", 2, "实收资本", 153, null, 4 },
                    { 621, 3, "4002", 2, "资本公积", 154, null, 4 },
                    { 622, 3, "400201", 2, "资本溢价", 155, "4002", 4 },
                    { 623, 3, "400202", 2, "股本溢价", 156, "4002", 4 },
                    { 624, 3, "400203", 2, "其他资本公积", 157, "4002", 4 },
                    { 625, 3, "4003", 2, "其他综合收益", 158, null, 4 },
                    { 626, 3, "4101", 2, "盈余公积", 159, null, 4 },
                    { 627, 3, "410101", 2, "法定盈余公积", 160, "4101", 4 },
                    { 628, 3, "410102", 2, "任意盈余公积", 161, "4101", 4 },
                    { 629, 3, "410103", 2, "法定公益金", 162, "4101", 4 },
                    { 630, 3, "410104", 2, "储备基金", 163, "4101", 4 },
                    { 631, 3, "410105", 2, "企业发展基金", 164, "4101", 4 },
                    { 632, 3, "410106", 2, "利润归还投资", 165, "4101", 4 },
                    { 633, 3, "4103", 2, "本年利润", 166, null, 4 },
                    { 634, 3, "4104", 2, "利润分配", 167, null, 4 },
                    { 635, 3, "410401", 1, "提取法定盈余公积", 168, "4104", 4 },
                    { 636, 3, "410402", 1, "提取任意盈余公积", 169, "4104", 4 },
                    { 637, 3, "410403", 1, "提取法定公益金", 170, "4104", 4 },
                    { 638, 3, "410404", 1, "应付现金股利或利润", 171, "4104", 4 },
                    { 639, 3, "410405", 1, "转作股本的利润", 172, "4104", 4 },
                    { 640, 3, "410406", 1, "盈余公积补亏", 173, "4104", 4 },
                    { 641, 3, "410407", 1, "提取储备基金", 174, "4104", 4 },
                    { 642, 3, "410408", 1, "提取企业发展基金", 175, "4104", 4 },
                    { 643, 3, "410409", 1, "提取职工奖励及福利基金", 176, "4104", 4 },
                    { 644, 3, "410410", 1, "利润归还投资", 177, "4104", 4 },
                    { 645, 3, "410411", 2, "未分配利润", 178, "4104", 4 },
                    { 646, 3, "4201", 1, "库存股", 179, null, 4 },
                    { 647, 4, "5001", 1, "生产成本", 180, null, 4 },
                    { 648, 4, "500101", 1, "基本生产成本", 181, "5001", 4 },
                    { 649, 4, "500102", 1, "辅助生产成本", 182, "5001", 4 },
                    { 650, 4, "5101", 1, "制造费用", 183, null, 4 },
                    { 651, 4, "5201", 1, "劳务成本", 184, null, 4 },
                    { 652, 4, "5301", 1, "研发支出", 185, null, 4 },
                    { 653, 4, "530101", 1, "费用化支出", 186, "5301", 4 },
                    { 654, 4, "530102", 1, "资本化支出", 187, "5301", 4 },
                    { 655, 5, "6001", 2, "主营业务收入", 188, null, 4 },
                    { 656, 5, "6051", 2, "其他业务收入", 189, null, 4 },
                    { 657, 5, "6101", 2, "公允价值变动损益", 190, null, 4 },
                    { 658, 5, "6111", 2, "投资收益", 191, null, 4 },
                    { 659, 5, "6301", 2, "营业外收入", 192, null, 4 },
                    { 660, 5, "6401", 1, "主营业务成本", 193, null, 4 },
                    { 661, 5, "6402", 1, "其他业务成本", 194, null, 4 },
                    { 662, 5, "6403", 1, "税金及附加", 195, null, 4 },
                    { 663, 5, "6601", 1, "销售费用", 196, null, 4 },
                    { 664, 5, "660101", 1, "办公费", 197, "6601", 4 },
                    { 665, 5, "660102", 1, "房租", 198, "6601", 4 },
                    { 666, 5, "660103", 1, "物业管理费", 199, "6601", 4 },
                    { 667, 5, "660104", 1, "水电费", 200, "6601", 4 },
                    { 668, 5, "660105", 1, "交际应酬费", 201, "6601", 4 },
                    { 669, 5, "660106", 1, "市内交通费", 202, "6601", 4 },
                    { 670, 5, "660107", 1, "差旅费", 203, "6601", 4 },
                    { 671, 5, "660108", 1, "补助", 204, "6601", 4 },
                    { 672, 5, "660109", 1, "通讯费", 205, "6601", 4 },
                    { 673, 5, "660110", 1, "工资", 206, "6601", 4 },
                    { 674, 5, "660111", 1, "佣金", 207, "6601", 4 },
                    { 675, 5, "660112", 1, "保险金", 208, "6601", 4 },
                    { 676, 5, "660113", 1, "福利费", 209, "6601", 4 },
                    { 677, 5, "660114", 1, "包装费", 210, "6601", 4 },
                    { 678, 5, "660115", 1, "广告和业务宣传费", 211, "6601", 4 },
                    { 679, 5, "660116", 1, "商品维修费", 212, "6601", 4 },
                    { 680, 5, "660117", 1, "预计产品质量保证损失", 213, "6601", 4 },
                    { 681, 5, "660118", 1, "运输费", 214, "6601", 4 },
                    { 682, 5, "660119", 1, "装卸费", 215, "6601", 4 },
                    { 683, 5, "660120", 1, "折旧费", 216, "6601", 4 },
                    { 684, 5, "660121", 1, "计提福利", 217, "6601", 4 },
                    { 685, 5, "660122", 1, "职工教育经费", 218, "6601", 4 },
                    { 686, 5, "660123", 1, "业务招待费", 219, "6601", 4 },
                    { 687, 5, "660124", 1, "劳务费", 220, "6601", 4 },
                    { 688, 5, "660199", 1, "其他", 221, "6601", 4 },
                    { 689, 5, "6602", 1, "管理费用", 222, null, 4 },
                    { 690, 5, "660201", 1, "办公费", 223, "6602", 4 },
                    { 691, 5, "660202", 1, "房租", 224, "6602", 4 },
                    { 692, 5, "660203", 1, "物业管理费", 225, "6602", 4 },
                    { 693, 5, "660204", 1, "水电费", 226, "6602", 4 },
                    { 694, 5, "660205", 1, "交际应酬费", 227, "6602", 4 },
                    { 695, 5, "660206", 1, "市内交通费", 228, "6602", 4 },
                    { 696, 5, "660207", 1, "差旅费", 229, "6602", 4 },
                    { 697, 5, "660208", 1, "通讯费", 230, "6602", 4 },
                    { 698, 5, "660209", 1, "工资", 231, "6602", 4 },
                    { 699, 5, "660210", 1, "保险金", 232, "6602", 4 },
                    { 700, 5, "660211", 1, "福利费", 233, "6602", 4 },
                    { 701, 5, "660212", 1, "工会经费", 234, "6602", 4 },
                    { 702, 5, "660213", 1, "董事会费", 235, "6602", 4 },
                    { 703, 5, "660214", 1, "聘请中介机构费", 236, "6602", 4 },
                    { 704, 5, "660215", 1, "咨询费", 237, "6602", 4 },
                    { 705, 5, "660216", 1, "诉讼费", 238, "6602", 4 },
                    { 706, 5, "660217", 1, "房产税", 239, "6602", 4 },
                    { 707, 5, "660218", 1, "车船使用税", 240, "6602", 4 },
                    { 708, 5, "660219", 1, "土地使用税", 241, "6602", 4 },
                    { 709, 5, "660220", 1, "印花税", 242, "6602", 4 },
                    { 710, 5, "660221", 1, "技术转让费", 243, "6602", 4 },
                    { 711, 5, "660222", 1, "矿产资源补偿费", 244, "6602", 4 },
                    { 712, 5, "660223", 1, "研究费用", 245, "6602", 4 },
                    { 713, 5, "660224", 1, "排污费", 246, "6602", 4 },
                    { 714, 5, "660225", 1, "折旧费", 247, "6602", 4 },
                    { 715, 5, "660226", 1, "计提福利", 248, "6602", 4 },
                    { 716, 5, "660227", 1, "职工教育经费", 249, "6602", 4 },
                    { 717, 5, "660228", 1, "存货盘亏或盘盈", 250, "6602", 4 },
                    { 718, 5, "660229", 1, "交通费", 251, "6602", 4 },
                    { 719, 5, "660230", 1, "运杂费", 252, "6602", 4 },
                    { 720, 5, "660231", 1, "修理费", 253, "6602", 4 },
                    { 721, 5, "660232", 1, "公积金", 254, "6602", 4 },
                    { 722, 5, "660233", 1, "社保", 255, "6602", 4 },
                    { 723, 5, "660234", 1, "汽车费用", 256, "6602", 4 },
                    { 724, 5, "660235", 1, "服务费", 257, "6602", 4 },
                    { 725, 5, "660236", 1, "无形资产摊销", 258, "6602", 4 },
                    { 726, 5, "660237", 1, "招聘费", 259, "6602", 4 },
                    { 727, 5, "660238", 1, "业务招待费", 260, "6602", 4 },
                    { 728, 5, "660299", 1, "其他", 261, "6602", 4 },
                    { 729, 5, "6603", 1, "财务费用", 262, null, 4 },
                    { 730, 5, "660301", 1, "汇兑损益", 263, "6603", 4 },
                    { 731, 5, "660302", 1, "利息", 264, "6603", 4 },
                    { 732, 5, "660303", 1, "手续费", 265, "6603", 4 },
                    { 733, 5, "660304", 1, "账户管理费", 266, "6603", 4 },
                    { 734, 5, "660399", 1, "其他", 267, "6603", 4 },
                    { 735, 5, "6701", 1, "资产减值损失", 268, null, 4 },
                    { 736, 5, "6711", 1, "营业外支出", 269, null, 4 },
                    { 737, 5, "6801", 1, "所得税", 270, null, 4 },
                    { 738, 5, "680101", 1, "当期所得税费用", 271, "6801", 4 },
                    { 739, 5, "680102", 1, "递延所得税费用", 272, "6801", 4 },
                    { 740, 5, "6901", 1, "以前年度损益调整", 273, null, 4 },
                    { 741, 1, "1001", 1, "现金", 1, null, 3 },
                    { 742, 1, "1002", 1, "银行存款", 2, null, 3 },
                    { 743, 1, "1009", 1, "其他货币资金", 3, null, 3 },
                    { 744, 1, "1101", 1, "短期投资", 4, null, 3 },
                    { 745, 1, "1102", 2, "短期投资跌价准备", 5, null, 3 },
                    { 746, 1, "1111", 1, "应收票据", 6, null, 3 },
                    { 747, 1, "1121", 1, "应收账款", 7, null, 3 },
                    { 748, 1, "1122", 1, "其他应收款", 8, null, 3 },
                    { 749, 1, "1131", 2, "坏账准备", 9, null, 3 },
                    { 750, 1, "1141", 1, "预付账款", 10, null, 3 },
                    { 751, 1, "1201", 1, "存货", 11, null, 3 },
                    { 752, 1, "1202", 2, "存货跌价准备", 12, null, 3 },
                    { 753, 1, "1301", 1, "待摊费用", 13, null, 3 },
                    { 754, 1, "1401", 1, "长期股权投资", 14, null, 3 },
                    { 755, 1, "1402", 1, "长期债权投资", 15, null, 3 },
                    { 756, 1, "1421", 2, "长期投资减值准备", 16, null, 3 },
                    { 757, 1, "1501", 1, "固定资产", 17, null, 3 },
                    { 758, 1, "1502", 2, "累计折旧", 18, null, 3 },
                    { 759, 1, "1505", 1, "在建工程", 19, null, 3 },
                    { 760, 1, "1506", 1, "文物文化资产", 20, null, 3 },
                    { 761, 1, "1509", 1, "固定资产清理", 21, null, 3 },
                    { 762, 1, "1601", 1, "无形资产", 22, null, 3 },
                    { 763, 1, "1701", 1, "受托代理资产", 23, null, 3 },
                    { 764, 2, "2101", 2, "短期借款", 24, null, 3 },
                    { 765, 2, "2201", 2, "应付票据", 25, null, 3 },
                    { 766, 2, "2202", 2, "应付账款", 26, null, 3 },
                    { 767, 2, "2203", 2, "预收账款", 27, null, 3 },
                    { 768, 2, "2204", 2, "应付工资", 28, null, 3 },
                    { 769, 2, "2206", 2, "应交税金", 29, null, 3 },
                    { 770, 2, "220601", 2, "营业税", 30, "2206", 3 },
                    { 771, 2, "220602", 2, "增值税", 31, "2206", 3 },
                    { 772, 2, "220603", 2, "所得税", 32, "2206", 3 },
                    { 773, 2, "220604", 2, "房产税", 33, "2206", 3 },
                    { 774, 2, "220605", 2, "个人所得税", 34, "2206", 3 },
                    { 775, 2, "2209", 2, "其他应付款", 35, null, 3 },
                    { 776, 2, "2301", 2, "预提费用", 36, null, 3 },
                    { 777, 2, "2401", 2, "预计负债", 37, null, 3 },
                    { 778, 2, "2501", 2, "长期借款", 38, null, 3 },
                    { 779, 2, "2502", 2, "长期应付款", 39, null, 3 },
                    { 780, 2, "2601", 2, "受托代理负债", 40, null, 3 },
                    { 781, 3, "3101", 2, "非限定性净资产", 41, null, 3 },
                    { 782, 3, "3102", 2, "限定性净资产", 42, null, 3 },
                    { 783, 5, "4101", 2, "捐赠收入", 43, null, 3 },
                    { 784, 5, "4201", 2, "会费收入", 44, null, 3 },
                    { 785, 5, "420101", 2, "团体会费", 45, "4201", 3 },
                    { 786, 5, "420102", 2, "个人会费", 46, "4201", 3 },
                    { 787, 5, "4301", 2, "提供服务收入", 47, null, 3 },
                    { 788, 5, "430101", 2, "学杂费收入", 48, "4301", 3 },
                    { 789, 5, "430102", 2, "医疗收入", 49, "4301", 3 },
                    { 790, 5, "430103", 2, "培训收入", 50, "4301", 3 },
                    { 791, 5, "4401", 2, "政府补助收入", 51, null, 3 },
                    { 792, 5, "4501", 2, "商品销售收入", 52, null, 3 },
                    { 793, 5, "4601", 2, "投资收益", 53, null, 3 },
                    { 794, 5, "4901", 2, "其他收入", 54, null, 3 },
                    { 795, 5, "5101", 1, "业务活动成本", 55, null, 3 },
                    { 796, 5, "5201", 1, "管理费用", 56, null, 3 },
                    { 797, 5, "520101", 1, "工资奖金", 57, "5201", 3 },
                    { 798, 5, "520102", 1, "福利费", 58, "5201", 3 },
                    { 799, 5, "520103", 1, "社会保障费", 59, "5201", 3 },
                    { 800, 5, "520104", 1, "办公费", 60, "5201", 3 },
                    { 801, 5, "520105", 1, "水电费", 61, "5201", 3 },
                    { 802, 5, "520106", 1, "差旅费", 62, "5201", 3 },
                    { 803, 5, "520107", 1, "折旧费", 63, "5201", 3 },
                    { 804, 5, "520108", 1, "离退休人员工资补助", 64, "5201", 3 },
                    { 805, 5, "520109", 1, "中介费", 65, "5201", 3 },
                    { 806, 5, "520110", 1, "应偿还受赠资产", 66, "5201", 3 },
                    { 807, 5, "520111", 1, "业务招待费", 67, "5201", 3 },
                    { 808, 5, "520112", 1, "广告费和业务宣传费", 68, "5201", 3 },
                    { 809, 5, "520113", 1, "通讯费", 69, "5201", 3 },
                    { 810, 5, "520114", 1, "修理费", 70, "5201", 3 },
                    { 811, 5, "520115", 1, "租赁费", 71, "5201", 3 },
                    { 812, 5, "520116", 1, "公积金", 72, "5201", 3 },
                    { 813, 5, "520117", 1, "汽车费用", 73, "5201", 3 },
                    { 814, 5, "520118", 1, "待摊费用摊销", 74, "5201", 3 },
                    { 815, 5, "520119", 1, "工会经费", 75, "5201", 3 },
                    { 816, 5, "520120", 1, "研究费用", 76, "5201", 3 },
                    { 817, 5, "520199", 1, "其他", 77, "5201", 3 },
                    { 818, 5, "5301", 1, "筹资费用", 78, null, 3 },
                    { 819, 5, "530101", 1, "借款费用", 79, "5301", 3 },
                    { 820, 5, "530102", 1, "汇兑损失", 80, "5301", 3 },
                    { 821, 5, "530103", 1, "活动费", 81, "5301", 3 },
                    { 822, 5, "530104", 1, "宣传资料费", 82, "5301", 3 },
                    { 823, 5, "530105", 1, "其他", 83, "5301", 3 },
                    { 824, 5, "5401", 1, "其他费用", 84, null, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_assist_types_account_id_type",
                table: "account_assist_types",
                columns: new[] { "account_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_currencies_account_id_currency_id",
                table: "account_currencies",
                columns: new[] { "account_id", "currency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_currencies_currency_id",
                table: "account_currencies",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_set_members_account_set_id_phone",
                table: "account_set_members",
                columns: new[] { "account_set_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "IX_account_set_members_role_id",
                table: "account_set_members",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_set_members_user_id",
                table: "account_set_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_set_roles_account_set_id_name",
                table: "account_set_roles",
                columns: new[] { "account_set_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_sets_is_current",
                table: "account_sets",
                column: "is_current");

            migrationBuilder.CreateIndex(
                name: "IX_account_sets_name",
                table: "account_sets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_account_set_id_code",
                table: "accounts",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_parent_id",
                table: "accounts",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_archive_categories_account_set_id_parent_id",
                table: "archive_categories",
                columns: new[] { "account_set_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_archive_categories_parent_id",
                table: "archive_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_archive_items_account_set_id_is_archived",
                table: "archive_items",
                columns: new[] { "account_set_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "IX_assist_items_account_set_id_type",
                table: "assist_items",
                columns: new[] { "account_set_id", "type" });

            migrationBuilder.CreateIndex(
                name: "IX_assist_items_account_set_id_type_code",
                table: "assist_items",
                columns: new[] { "account_set_id", "type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assist_items_parent_id",
                table: "assist_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_bill_attachments_bill_id",
                table: "bill_attachments",
                column: "bill_id");

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
                name: "IX_budget_lines_account_set_id",
                table: "budget_lines",
                column: "account_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_lines_budget_id_account_id",
                table: "budget_lines",
                columns: new[] { "budget_id", "account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budgets_account_set_id_year_name",
                table: "budgets",
                columns: new[] { "account_set_id", "year", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_flow_formulas_account_set_id_line_no",
                table: "cash_flow_formulas",
                columns: new[] { "account_set_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_closing_template_lines_template_id",
                table: "closing_template_lines",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_closing_templates_account_set_id_name",
                table: "closing_templates",
                columns: new[] { "account_set_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_currencies_account_set_id_code",
                table: "currencies",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_reports_account_set_id_code",
                table: "custom_reports",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_insurances_employee_id_insurance_item_id",
                table: "employee_insurances",
                columns: new[] { "employee_id", "insurance_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_insurances_insurance_item_id",
                table: "employee_insurances",
                column: "insurance_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_employees_account_set_id_code",
                table: "employees",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_account_set_id_type_code",
                table: "entry_categories",
                columns: new[] { "account_set_id", "type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_account_set_id_type_name",
                table: "entry_categories",
                columns: new[] { "account_set_id", "type", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_bills_account_set_id_bill_date",
                table: "expense_bills",
                columns: new[] { "account_set_id", "bill_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expense_bills_account_set_id_bill_no",
                table: "expense_bills",
                columns: new[] { "account_set_id", "bill_no" },
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
                name: "IX_fixed_asset_voucher_settings_account_set_id",
                table: "fixed_asset_voucher_settings",
                column: "account_set_id",
                unique: true);

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
                name: "IX_journal_entries_journal_id",
                table: "journal_entries",
                column: "journal_id");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_voucher_id",
                table: "journal_entries",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_journals_account_set_id_account_id",
                table: "journals",
                columns: new[] { "account_set_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_operation_logs_account_set_id_time",
                table: "operation_logs",
                columns: new[] { "account_set_id", "time" });

            migrationBuilder.CreateIndex(
                name: "IX_preset_accounts_standard_code",
                table: "preset_accounts",
                columns: new[] { "standard", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preset_accounts_standard_order_no",
                table: "preset_accounts",
                columns: new[] { "standard", "order_no" });

            migrationBuilder.CreateIndex(
                name: "IX_salary_bills_account_set_id_period_employee_id",
                table: "salary_bills",
                columns: new[] { "account_set_id", "period", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_salary_voucher_settings_account_set_id",
                table: "salary_voucher_settings",
                column: "account_set_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_tasks_account_set_id_submitted_at",
                table: "tax_tasks",
                columns: new[] { "account_set_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_account_set_id_date",
                table: "transfers",
                columns: new[] { "account_set_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_users_phone",
                table: "users",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_attachments_voucher_id",
                table: "voucher_attachments",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entries_account_id",
                table: "voucher_entries",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entries_voucher_id",
                table: "voucher_entries",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entry_assists_account_id_assist_item_id",
                table: "voucher_entry_assists",
                columns: new[] { "account_id", "assist_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entry_assists_assist_item_id",
                table: "voucher_entry_assists",
                column: "assist_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entry_assists_voucher_entry_id",
                table: "voucher_entry_assists",
                column: "voucher_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entry_assists_voucher_entry_id_type_assist_item_id",
                table: "voucher_entry_assists",
                columns: new[] { "voucher_entry_id", "type", "assist_item_id" },
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

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_account_set_id_word_number_date",
                table: "vouchers",
                columns: new[] { "account_set_id", "word", "number", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_assist_types");

            migrationBuilder.DropTable(
                name: "account_currencies");

            migrationBuilder.DropTable(
                name: "account_set_members");

            migrationBuilder.DropTable(
                name: "archive_categories");

            migrationBuilder.DropTable(
                name: "archive_items");

            migrationBuilder.DropTable(
                name: "bill_attachments");

            migrationBuilder.DropTable(
                name: "budget_lines");

            migrationBuilder.DropTable(
                name: "cash_flow_formulas");

            migrationBuilder.DropTable(
                name: "closing_template_lines");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "custom_reports");

            migrationBuilder.DropTable(
                name: "employee_insurances");

            migrationBuilder.DropTable(
                name: "entry_categories");

            migrationBuilder.DropTable(
                name: "expense_bills");

            migrationBuilder.DropTable(
                name: "fixed_asset_categories");

            migrationBuilder.DropTable(
                name: "fixed_asset_changes");

            migrationBuilder.DropTable(
                name: "fixed_asset_voucher_settings");

            migrationBuilder.DropTable(
                name: "fixed_assets");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "operation_logs");

            migrationBuilder.DropTable(
                name: "preset_accounts");

            migrationBuilder.DropTable(
                name: "salary_bills");

            migrationBuilder.DropTable(
                name: "salary_voucher_settings");

            migrationBuilder.DropTable(
                name: "tax_portal_sessions");

            migrationBuilder.DropTable(
                name: "tax_tasks");

            migrationBuilder.DropTable(
                name: "transfers");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "voucher_attachments");

            migrationBuilder.DropTable(
                name: "voucher_entry_assists");

            migrationBuilder.DropTable(
                name: "voucher_template_lines");

            migrationBuilder.DropTable(
                name: "voucher_words");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "account_set_roles");

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropTable(
                name: "budgets");

            migrationBuilder.DropTable(
                name: "closing_templates");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "insurance_items");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "journals");

            migrationBuilder.DropTable(
                name: "assist_items");

            migrationBuilder.DropTable(
                name: "voucher_entries");

            migrationBuilder.DropTable(
                name: "voucher_templates");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropTable(
                name: "account_sets");
        }
    }
}
