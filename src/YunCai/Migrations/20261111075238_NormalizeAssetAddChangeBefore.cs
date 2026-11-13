using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAssetAddChangeBefore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 新增变更记录的「变动前内容」与柠檬云一致回填原值（此前插入的历史行 before 为空）。
            migrationBuilder.Sql(@"
UPDATE fixed_asset_changes
SET before = after
WHERE kind = 5 AND before = '';
");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
