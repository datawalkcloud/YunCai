using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountCodingScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_coding_scheme",
                table: "account_sets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "4-2-2-2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "account_coding_scheme",
                table: "account_sets");
        }
    }
}
