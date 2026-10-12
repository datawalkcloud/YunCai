using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class AddClosingTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "closing_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closing_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_closing_templates_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "closing_template_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closing_template_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_closing_template_lines_closing_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "closing_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_closing_template_lines_template_id",
                table: "closing_template_lines",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_closing_templates_account_set_id_name",
                table: "closing_templates",
                columns: new[] { "account_set_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "closing_template_lines");

            migrationBuilder.DropTable(
                name: "closing_templates");
        }
    }
}
