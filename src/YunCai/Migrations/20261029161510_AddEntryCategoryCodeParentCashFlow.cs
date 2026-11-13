using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryCategoryCodeParentCashFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cash_flow_item_id",
                table: "entry_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "entry_categories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "entry_categories",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "entry_categories",
                type: "uuid",
                nullable: true);

            // 存量类别按账套内「类型 + 名称」顺序回填编码，避免空串撞唯一索引。
            migrationBuilder.Sql(
                """
                UPDATE entry_categories ec
                SET code = sub.rn::text
                FROM (
                    SELECT id, row_number() OVER (PARTITION BY account_set_id ORDER BY type, name) AS rn
                    FROM entry_categories
                ) sub
                WHERE ec.id = sub.id AND (ec.code IS NULL OR ec.code = '');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "entry_categories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_account_set_id_code",
                table: "entry_categories",
                columns: new[] { "account_set_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entry_categories_account_set_id_code",
                table: "entry_categories");

            migrationBuilder.DropColumn(
                name: "cash_flow_item_id",
                table: "entry_categories");

            migrationBuilder.DropColumn(
                name: "code",
                table: "entry_categories");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "entry_categories");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "entry_categories");
        }
    }
}
