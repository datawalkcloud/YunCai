using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSetRoles : Migration
    {
        // 预置角色权限（与 RolePermissionCatalog.Presets 保持一致）。
        private const string AdminPermissions =
            "vouchers.view,vouchers.edit,vouchers.import,vouchers.export,vouchers.delete,vouchers.print," +
            "funds.view,funds.edit,funds.import,funds.export,funds.print," +
            "invoices.view,invoices.edit,invoices.import,invoices.export," +
            "salary.view,salary.edit,assets.view,assets.edit,tax.view,tax.edit,closing.view,closing.edit," +
            "ledgers.view,ledgers.export,ledgers.print,reports.view,reports.export,reports.print," +
            "accountset-add.execute,accountset-delete.execute,backup.view,backup.edit,reinit.execute," +
            "link-inventory.execute,link-invoice.execute";

        private const string SupervisorPermissions =
            "vouchers.view,vouchers.edit,vouchers.import,vouchers.export,vouchers.delete,vouchers.print," +
            "funds.view,funds.edit,funds.import,funds.export,funds.print," +
            "invoices.view,invoices.edit,invoices.import,invoices.export," +
            "salary.view,salary.edit,assets.view,assets.edit,tax.view,tax.edit,closing.view,closing.edit," +
            "ledgers.view,ledgers.export,ledgers.print,reports.view,reports.export,reports.print," +
            "accountset-add.execute";

        private const string MakerPermissions =
            "vouchers.view,vouchers.edit,vouchers.import,vouchers.export,vouchers.delete,vouchers.print," +
            "invoices.view,invoices.edit,invoices.import,invoices.export," +
            "salary.view,salary.edit,assets.view,assets.edit,tax.view,tax.edit,closing.view,closing.edit," +
            "funds.view,ledgers.view,reports.view,accountset-add.execute";

        private const string CashierPermissions =
            "funds.view,funds.edit,funds.import,funds.export,funds.print," +
            "vouchers.view,assets.view,ledgers.view,reports.view,accountset-add.execute";

        private const string ViewerPermissions =
            "vouchers.view,funds.view,assets.view,ledgers.view,reports.view,accountset-add.execute";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "account_set_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_set_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    permissions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_set_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_set_roles_account_sets_account_set_id",
                        column: x => x.account_set_id,
                        principalTable: "account_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_set_roles_account_set_id_name",
                table: "account_set_roles",
                columns: new[] { "account_set_id", "name" },
                unique: true);

            // 为所有既有账套播种 5 个预置角色；id 由账套 + 角色名确定性生成，便于回滚与重跑。
            migrationBuilder.Sql($@"
INSERT INTO account_set_roles (id, account_set_id, name, is_system, is_admin, permissions, sort_order, created_at, updated_at)
SELECT (md5(a.id::text || ':role:账套管理员'))::uuid, a.id, '账套管理员', TRUE, TRUE, '{AdminPermissions}', 0, now(), now()
FROM account_sets a
WHERE NOT EXISTS (SELECT 1 FROM account_set_roles r WHERE r.account_set_id = a.id AND r.name = '账套管理员');
INSERT INTO account_set_roles (id, account_set_id, name, is_system, is_admin, permissions, sort_order, created_at, updated_at)
SELECT (md5(a.id::text || ':role:主管'))::uuid, a.id, '主管', TRUE, FALSE, '{SupervisorPermissions}', 1, now(), now()
FROM account_sets a
WHERE NOT EXISTS (SELECT 1 FROM account_set_roles r WHERE r.account_set_id = a.id AND r.name = '主管');
INSERT INTO account_set_roles (id, account_set_id, name, is_system, is_admin, permissions, sort_order, created_at, updated_at)
SELECT (md5(a.id::text || ':role:制单人'))::uuid, a.id, '制单人', TRUE, FALSE, '{MakerPermissions}', 2, now(), now()
FROM account_sets a
WHERE NOT EXISTS (SELECT 1 FROM account_set_roles r WHERE r.account_set_id = a.id AND r.name = '制单人');
INSERT INTO account_set_roles (id, account_set_id, name, is_system, is_admin, permissions, sort_order, created_at, updated_at)
SELECT (md5(a.id::text || ':role:出纳'))::uuid, a.id, '出纳', TRUE, FALSE, '{CashierPermissions}', 3, now(), now()
FROM account_sets a
WHERE NOT EXISTS (SELECT 1 FROM account_set_roles r WHERE r.account_set_id = a.id AND r.name = '出纳');
INSERT INTO account_set_roles (id, account_set_id, name, is_system, is_admin, permissions, sort_order, created_at, updated_at)
SELECT (md5(a.id::text || ':role:查看'))::uuid, a.id, '查看', TRUE, FALSE, '{ViewerPermissions}', 4, now(), now()
FROM account_sets a
WHERE NOT EXISTS (SELECT 1 FROM account_set_roles r WHERE r.account_set_id = a.id AND r.name = '查看');
");

            // 旧枚举 → 新角色：0 管理员 / 1 会计（归入制单人）/ 2 出纳 / 3 查看。
            migrationBuilder.Sql(@"
UPDATE account_set_members m
SET role_id = r.id
FROM account_set_roles r
WHERE r.account_set_id = m.account_set_id
  AND r.name = CASE m.role WHEN 0 THEN '账套管理员' WHEN 1 THEN '制单人' WHEN 2 THEN '出纳' ELSE '查看' END;
");

            // 兜底：任何没匹配到角色的成员归入「查看」。
            migrationBuilder.Sql(@"
UPDATE account_set_members m
SET role_id = r.id
FROM account_set_roles r
WHERE m.role_id IS NULL AND r.account_set_id = m.account_set_id AND r.name = '查看';
");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "account_set_members",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "role",
                table: "account_set_members");

            migrationBuilder.CreateIndex(
                name: "IX_account_set_members_role_id",
                table: "account_set_members",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_set_members_account_set_roles_role_id",
                table: "account_set_members",
                column: "role_id",
                principalTable: "account_set_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_set_members_account_set_roles_role_id",
                table: "account_set_members");

            migrationBuilder.DropIndex(
                name: "IX_account_set_members_role_id",
                table: "account_set_members");

            migrationBuilder.AddColumn<int>(
                name: "role",
                table: "account_set_members",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.Sql(@"
UPDATE account_set_members m
SET role = CASE r.name
        WHEN '账套管理员' THEN 0
        WHEN '制单人' THEN 1
        WHEN '出纳' THEN 2
        ELSE 3
    END
FROM account_set_roles r
WHERE r.id = m.role_id;
");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "account_set_members");

            migrationBuilder.DropTable(
                name: "account_set_roles");
        }
    }
}
