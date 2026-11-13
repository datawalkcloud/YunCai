using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class EntryCategoryCodePerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entry_categories_account_set_id_code",
                table: "entry_categories");

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_account_set_id_type_code",
                table: "entry_categories",
                columns: new[] { "account_set_id", "type", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entry_categories_account_set_id_type_code",
                table: "entry_categories");

            migrationBuilder.CreateIndex(
                name: "IX_entry_categories_account_set_id_code",
                table: "entry_categories",
                columns: new[] { "account_set_id", "code" },
                unique: true);
        }
    }
}
