using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bill_no",
                table: "journal_entries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remark",
                table: "journal_entries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "settlement_method",
                table: "journal_entries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "journal_entries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bill_no",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "remark",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "settlement_method",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "source",
                table: "journal_entries");
        }
    }
}
