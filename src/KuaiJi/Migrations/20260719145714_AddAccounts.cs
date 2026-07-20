using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    is_leaf = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_preset = table.Column<bool>(type: "boolean", nullable: false),
                    mnemonic = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_accounts_parent_id",
                        column: x => x.parent_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_code",
                table: "accounts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_parent_id",
                table: "accounts",
                column: "parent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
