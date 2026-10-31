using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPartiesAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "acceptor_account_no",
                table: "bills",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "acceptor_bank",
                table: "bills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "drawer_account_no",
                table: "bills",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "drawer_bank",
                table: "bills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payee",
                table: "bills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payee_account_no",
                table: "bills",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payee_bank",
                table: "bills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "voucher_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bill_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_bill_attachments_bills_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_attachments_bill_id",
                table: "bill_attachments",
                column: "bill_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_attachments");

            migrationBuilder.DropColumn(
                name: "acceptor_account_no",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "acceptor_bank",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "drawer_account_no",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "drawer_bank",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "payee",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "payee_account_no",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "payee_bank",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "voucher_id",
                table: "bills");
        }
    }
}
