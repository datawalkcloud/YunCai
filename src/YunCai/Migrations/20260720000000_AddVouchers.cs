using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    attachment_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    preparer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vouchers", x => x.id);
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

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entries_account_id",
                table: "voucher_entries",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_entries_voucher_id",
                table: "voucher_entries",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_vouchers_word_number_date",
                table: "vouchers",
                columns: new[] { "word", "number", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_entries");

            migrationBuilder.DropTable(
                name: "vouchers");
        }
    }
}
