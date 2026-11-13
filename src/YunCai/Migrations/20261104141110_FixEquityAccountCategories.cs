using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <summary>
    /// 凭证导入自动建科目时，4xxx 一律按「成本」推断，导致 2007 科目表里的权益科目
    /// （实收资本 4001、本年利润 4103 等）被计成存货、报表不平。这里按「编码 + 名称」精确修正存量：
    /// 只改动名称命中权益关键字的一级科目及其下级，不会误伤旧准则/小企业科目表中的 4001 生产成本。
    /// </summary>
    public partial class FixEquityAccountCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE accounts AS a
                SET category = 3, direction = 2
                WHERE a.category = 4
                  AND EXISTS (
                      SELECT 1
                      FROM accounts AS p
                      WHERE p.account_set_id = a.account_set_id
                        AND length(p.code) = 4
                        AND a.code LIKE p.code || '%'
                        AND p.name IN (
                            '实收资本', '股本', '资本公积', '其他权益工具', '其他综合收益',
                            '专项储备', '盈余公积', '一般风险准备', '本年利润', '利润分配', '库存股'
                        )
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 原始类别无法区分（可能与真实成本科目混淆），回滚不做反向改写。
        }
    }
}
