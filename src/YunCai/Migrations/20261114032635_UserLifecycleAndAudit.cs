using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YunCai.Migrations
{
    /// <inheritdoc />
    public partial class UserLifecycleAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_system_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "after_json",
                table: "operation_logs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "before_json",
                table: "operation_logs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_id",
                table: "operation_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "operation_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "operation_logs",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "permissions",
                table: "account_set_roles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            // 存量数据：预置管理员账号提升为系统管理员（与运行时 DatabaseBootstrap 的兜底一致）。
            migrationBuilder.Sql("UPDATE users SET is_system_admin = true WHERE username = 'admin';");

            // 存量预置角色补齐新增模块的权限键；仅按预置角色名回填，自定义角色保持原样。
            migrationBuilder.Sql(@"
UPDATE account_set_roles SET permissions = (
    SELECT string_agg(k, ',' ORDER BY k) FROM (
        SELECT DISTINCT unnest(string_to_array(permissions, ',') || ARRAY[
            'accounts.view','accounts.edit','accounts.import','accounts.export',
            'assist.view','assist.edit',
            'archives.view','archives.edit',
            'settings.view','settings.edit',
            'operationlog.view',
            'budget.view','budget.edit',
            'reports.edit']) AS k
    ) t WHERE k <> ''
) WHERE name = '主管';

UPDATE account_set_roles SET permissions = (
    SELECT string_agg(k, ',' ORDER BY k) FROM (
        SELECT DISTINCT unnest(string_to_array(permissions, ',') || ARRAY[
            'accounts.view','assist.view','settings.view','operationlog.view',
            'budget.view','budget.edit']) AS k
    ) t WHERE k <> ''
) WHERE name = '制单人';

UPDATE account_set_roles SET permissions = (
    SELECT string_agg(k, ',' ORDER BY k) FROM (
        SELECT DISTINCT unnest(string_to_array(permissions, ',') || ARRAY[
            'accounts.view','assist.view','archives.view','settings.view',
            'operationlog.view','budget.view']) AS k
    ) t WHERE k <> ''
) WHERE name = '出纳';

UPDATE account_set_roles SET permissions = (
    SELECT string_agg(k, ',' ORDER BY k) FROM (
        SELECT DISTINCT unnest(string_to_array(permissions, ',') || ARRAY[
            'accounts.view','assist.view','archives.view','settings.view',
            'operationlog.view','budget.view']) AS k
    ) t WHERE k <> ''
) WHERE name = '查看';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_system_admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "after_json",
                table: "operation_logs");

            migrationBuilder.DropColumn(
                name: "before_json",
                table: "operation_logs");

            migrationBuilder.DropColumn(
                name: "entity_id",
                table: "operation_logs");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "operation_logs");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "operation_logs");

            migrationBuilder.AlterColumn<string>(
                name: "permissions",
                table: "account_set_roles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
