using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxPortalSessionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_tax_portal_sessions",
                table: "tax_portal_sessions");

            migrationBuilder.AddColumn<int>(
                name: "portal_kind",
                table: "tax_portal_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_tax_portal_sessions",
                table: "tax_portal_sessions",
                columns: new[] { "account_set_id", "user_id", "portal_kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_tax_portal_sessions",
                table: "tax_portal_sessions");

            migrationBuilder.DropColumn(
                name: "portal_kind",
                table: "tax_portal_sessions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tax_portal_sessions",
                table: "tax_portal_sessions",
                columns: new[] { "account_set_id", "user_id" });
        }
    }
}
