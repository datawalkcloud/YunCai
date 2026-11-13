using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "enabled_assist_types",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.CreateIndex(
                name: "IX_account_assist_types_account_id_type",
                table: "account_assist_types",
                columns: new[] { "account_id", "type" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_assist_types");

            migrationBuilder.DropTable(
                name: "voucher_entry_assists");

            migrationBuilder.DropTable(
                name: "assist_items");

            migrationBuilder.DropColumn(
                name: "enabled_assist_types",
                table: "account_sets");
        }
    }
}
