using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <summary>
    /// 凭证工作流简化为「制单 → 过账」后，原「已审核」状态（2）不复存在：
    /// 存量已审核凭证一律转为已过账（3），使其正常进入账簿与报表。
    /// </summary>
    public partial class ConvertReviewedToPosted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE vouchers SET status = 3 WHERE status = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 无法区分「原生过账」与「由已审核转换而来」，回滚不做反向改写。
        }
    }
}
