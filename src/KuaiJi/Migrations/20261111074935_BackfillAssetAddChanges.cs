using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KuaiJi.Migrations
{
    /// <inheritdoc />
    public partial class BackfillAssetAddChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 老卡片补一条「新增」变更记录（与新建卡片时的行为一致），期间取开始使用日期所在月。
            migrationBuilder.Sql(@"
INSERT INTO fixed_asset_changes (id, account_set_id, asset_id, kind, before, after, period, amount, created_at, voucher_id)
SELECT gen_random_uuid(), a.account_set_id, a.id, 5, to_char(a.origin_value, 'FM999999999999999990.00'),
       to_char(a.origin_value, 'FM999999999999999990.00'),
       EXTRACT(YEAR FROM a.start_date)::int * 100 + EXTRACT(MONTH FROM a.start_date)::int,
       a.origin_value, now(), NULL
FROM fixed_assets a
WHERE NOT EXISTS (
    SELECT 1 FROM fixed_asset_changes c WHERE c.asset_id = a.id AND c.kind = 5
);
");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM fixed_asset_changes WHERE kind = 5;");
        }

    }
}
