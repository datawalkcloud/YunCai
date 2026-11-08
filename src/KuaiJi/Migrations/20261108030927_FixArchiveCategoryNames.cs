using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class FixArchiveCategoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 播种逻辑早期把内置分类写成「现金流」，与项目既有命名（以及柠檬云参考页面）不一致。
            // 只修正挂在「所有文件」下的系统内置节点，幂等且不影响用户自建分类。
            migrationBuilder.Sql("""
                UPDATE archive_categories AS c
                SET name = '现金流水'
                WHERE c.is_system AND c.name = '现金流'
                  AND EXISTS (
                      SELECT 1 FROM archive_categories AS p
                      WHERE p.id = c.parent_id AND p.name = '所有文件'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
