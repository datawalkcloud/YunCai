using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalCodeCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "journals",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "journals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "人民币");

            // 存量日记账编码回填：按 (账套, 类型) 分区顺序取号，三位补零，与 NextCodeAsync 规则一致。
            migrationBuilder.Sql("""
                UPDATE journals j
                SET code = LPAD(sub.rn::text, 3, '0')
                FROM (
                    SELECT id, row_number() OVER (PARTITION BY account_set_id, type ORDER BY name) AS rn
                    FROM journals
                    WHERE code IS NULL OR code = ''
                ) sub
                WHERE j.id = sub.id AND (j.code IS NULL OR j.code = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "code",
                table: "journals");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "journals");
        }
    }
}
