using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entry_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_account_set_id_type_name",
                table: "entry_categories",
                columns: new[] { "account_set_id", "type", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_categories");
        }
    }
}
