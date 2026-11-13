using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSets : Migration
    {
        // 用于回填存量数据的固定默认账套标识。存量的科目与凭证在本次迁移后统一归入该账套。
        private static readonly Guid DefaultAccountSetId = new("11111111-1111-1111-1111-111111111111");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 账套表：科目、日记账、凭证、报表的归属边界。
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
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_sets", x => x.id);
                });

            // 2) 写入固定标识的默认账套，供存量数据回填与首次启动使用（置为当前账套）。
            migrationBuilder.InsertData(
                table: "account_sets",
                columns: new[] { "id", "name", "company_name", "standard", "taxpayer_type", "base_currency", "start_year", "start_month", "is_current" },
                values: new object[] { DefaultAccountSetId, "默认账套", "默认单位", 1, 1, "人民币", 2026, 1, true });

            // 3) 为存量科目补充账套字段：先以默认账套回填，再去除数据库默认值以与模型保持一致。
            migrationBuilder.AddColumn<Guid>(
                name: "account_set_id",
                table: "accounts",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultAccountSetId);
            migrationBuilder.Sql("ALTER TABLE accounts ALTER COLUMN account_set_id DROP DEFAULT;");

            // 4) 为存量凭证补充账套字段：同样先回填再去除默认值。
            migrationBuilder.AddColumn<Guid>(
                name: "account_set_id",
                table: "vouchers",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultAccountSetId);
            migrationBuilder.Sql("ALTER TABLE vouchers ALTER COLUMN account_set_id DROP DEFAULT;");

            // 5) 日记账表：归属于账套（Restrict）。
            migrationBuilder.CreateTable(
                name: "journals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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

            // 6) 日记账分录表：归属于日记账（Cascade）。
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
                    income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    expense = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true)
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

            // 7) 科目编码由「全局唯一」改为「账套内唯一」。
            migrationBuilder.DropIndex(
                name: "IX_accounts_code",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_account_set_id_code",
                table: "accounts",
                columns: new[] { "account_set_id", "code" },
                unique: true);

            // 8) 凭证检索/去重索引加入账套维度。
            migrationBuilder.DropIndex(
                name: "IX_vouchers_word_number_date",
                table: "vouchers");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_account_set_id_word_number_date",
                table: "vouchers",
                columns: new[] { "account_set_id", "word", "number", "date" });

            // 9) 日记账及其分录的索引。
            migrationBuilder.CreateIndex(
                name: "IX_journals_account_set_id_account_id",
                table: "journals",
                columns: new[] { "account_set_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_journal_id",
                table: "journal_entries",
                column: "journal_id");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_voucher_id",
                table: "journal_entries",
                column: "voucher_id");

            // 10) 科目、凭证归属账套的外键（Restrict：账套非空时不可删除）。
            migrationBuilder.AddForeignKey(
                name: "FK_accounts_account_sets_account_set_id",
                table: "accounts",
                column: "account_set_id",
                principalTable: "account_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vouchers_account_sets_account_set_id",
                table: "vouchers",
                column: "account_set_id",
                principalTable: "account_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_account_sets_account_set_id",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_vouchers_account_sets_account_set_id",
                table: "vouchers");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "journals");

            migrationBuilder.DropIndex(
                name: "IX_accounts_account_set_id_code",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_vouchers_account_set_id_word_number_date",
                table: "vouchers");

            migrationBuilder.DropColumn(
                name: "account_set_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "account_set_id",
                table: "vouchers");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_code",
                table: "accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_word_number_date",
                table: "vouchers",
                columns: new[] { "word", "number", "date" });

            migrationBuilder.DropTable(
                name: "account_sets");
        }
    }
}
