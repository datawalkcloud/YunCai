using System.Text.Json;
using KuaiJi.Modules.AccountSets.Application;
using KuaiJi.Modules.Auditing.Application;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KuaiJi.SharedKernel.Infrastructure.Data
{
    /// <summary>备份恢复结果。</summary>
    public sealed class BackupRestoreResultDto
    {
        /// <summary>各表恢复行数。</summary>
        public IReadOnlyDictionary<string, int> Restored { get; init; } = new Dictionary<string, int>();

        /// <summary>恢复的总行数。</summary>
        public int TotalRows { get; init; }

        /// <summary>备份创建时间。</summary>
        public DateTime? BackedUpAt { get; init; }
    }

    /// <summary>
    /// 账套备份恢复服务：把账套内全部业务表按行导出为 JSON 数据包，
    /// 恢复时先清空再按外键依赖顺序原样写回（保留原主键与期初余额）。
    /// 直接走 ADO.NET 读写，避免实体私有 setter 反序列化问题。
    /// </summary>
    public sealed class AccountSetBackupService
    {
        /// <summary>按外键依赖排序（父表在前）；恢复删除时倒序执行。</summary>
        private static readonly string[] OrderedTables =
        {
            "accounts",
            "currencies",
            "voucher_words",
            "entry_categories",
            "assist_items",
            "fixed_asset_categories",
            "fixed_asset_voucher_settings",
            "salary_voucher_settings",
            "employees",
            "insurance_items",
            "employee_insurances",
            "voucher_templates",
            "voucher_template_lines",
            "closing_templates",
            "closing_template_lines",
            "vouchers",
            "voucher_entries",
            "voucher_entry_assists",
            "journals",
            "journal_entries",
            "transfers",
            "bills",
            "invoices",
            "invoice_lines",
            "salary_bills",
            "fixed_assets",
            "fixed_asset_changes",
            "archive_items",
            "expense_bills",
            "custom_reports",
        };

        private readonly AppDbContext _context;
        private readonly OperationLogService _operationLogService;

        public AccountSetBackupService(AppDbContext context, OperationLogService operationLogService)
        {
            _context = context;
            _operationLogService = operationLogService;
        }

        /// <summary>导出账套全部业务数据为 JSON 数据包。</summary>
        public async Task<byte[]> ExportAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
                var package = new Dictionary<string, object?>
                {
                    ["version"] = 1,
                    ["accountSetId"] = accountSetId,
                    ["createdAt"] = DateTime.UtcNow,
                    ["tables"] = new Dictionary<string, List<Dictionary<string, object?>>>(),
                };
                var tables = (Dictionary<string, List<Dictionary<string, object?>>>)package["tables"]!;

                // 账套主表单独导（恢复时只取结账期间进度）。
                tables["account_sets"] = await DumpAsync(conn, "account_sets", "id", accountSetId, cancellationToken);
                foreach (var table in OrderedTables)
                {
                    var columns = await GetColumnsAsync(conn, table, cancellationToken);
                    if (columns.All(c => c.Name != "account_set_id"))
                    {
                        continue; // 没有账套外键的表不参与备份
                    }

                    tables[table] = await DumpAsync(conn, table, "account_set_id", accountSetId, cancellationToken);
                }

                return JsonSerializer.SerializeToUtf8Bytes(package,
                    new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        /// <summary>从备份数据包恢复：清空账套现有业务数据后按依赖顺序写回。</summary>
        public async Task<BackupRestoreResultDto> RestoreAsync(Guid accountSetId, Stream backupStream, string operatorUserId, string operatorUserName, CancellationToken cancellationToken = default)
        {
            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(backupStream, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw new ReinitException("备份文件格式不正确，请上传本系统导出的账套备份数据包。");
            }

            using var _ = document;
            var root = document.RootElement;
            if (!root.TryGetProperty("tables", out var tablesElement) || tablesElement.ValueKind != JsonValueKind.Object)
            {
                throw new ReinitException("备份文件缺少数据表内容，请确认是本系统导出的备份数据包。");
            }

            if (!root.TryGetProperty("accountSetId", out var backupIdElement) || backupIdElement.GetString() != accountSetId.ToString())
            {
                throw new ReinitException("备份文件属于其他账套，不能恢复到当前账套。");
            }

            var backedUpAt = root.TryGetProperty("createdAt", out var createdAtElement) && createdAtElement.ValueKind == JsonValueKind.String
                ? createdAtElement.GetDateTime()
                : (DateTime?)null;

            await _context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
                await using var tx = await conn.BeginTransactionAsync(cancellationToken);
                var restored = new Dictionary<string, int>();
                var total = 0;

                // 1. 倒序清空现有业务数据（含账套行外的所有表）。
                foreach (var table in OrderedTables.Reverse())
                {
                    var columns = await GetColumnsAsync(conn, table, cancellationToken);
                    if (columns.All(c => c.Name != "account_set_id"))
                    {
                        continue;
                    }

                    await using var delete = new NpgsqlCommand($"DELETE FROM \"{table}\" WHERE \"account_set_id\" = @id", conn, (NpgsqlTransaction)tx);
                    delete.Parameters.AddWithValue("id", accountSetId);
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                }

                // 2. 按依赖顺序写回备份数据。
                foreach (var table in OrderedTables)
                {
                    if (!tablesElement.TryGetProperty(table, out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var rows = rowsElement.EnumerateArray().ToList();
                    if (rows.Count == 0)
                    {
                        restored[table] = 0;
                        continue;
                    }

                    var columns = await GetColumnsAsync(conn, table, cancellationToken);
                    var first = rows[0].EnumerateObject().Select(p => p.Name).ToList();
                    if (first.Count == 0)
                    {
                        continue;
                    }

                    var names = string.Join(", ", first.Select(c => $"\"{c}\""));
                    var parameters = string.Join(", ", first.Select((c, i) =>
                    {
                        var type = columns.FirstOrDefault(x => x.Name == c).Type ?? "text";
                        return $"@p{i}{Cast(type)}";
                    }));
                    var sql = $"INSERT INTO \"{table}\" ({names}) VALUES ({parameters})";

                    foreach (var row in rows)
                    {
                        await using var insert = new NpgsqlCommand(sql, conn, (NpgsqlTransaction)tx);
                        var i = 0;
                        foreach (var name in first)
                        {
                            var value = row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var v) ? v : (JsonElement?)null;
                            insert.Parameters.AddWithValue($"p{i}", ToDbValue(value));
                            i++;
                        }

                        await insert.ExecuteNonQueryAsync(cancellationToken);
                    }

                    restored[table] = rows.Count;
                    total += rows.Count;
                }

                // 3. 恢复账套结账进度。
                if (tablesElement.TryGetProperty("account_sets", out var setsElement) && setsElement.GetArrayLength() > 0)
                {
                    var set = setsElement[0];
                    var carried = set.TryGetProperty("carried_period", out var c) ? c.GetInt32() : 0;
                    var closed = set.TryGetProperty("closed_period", out var cl) ? cl.GetInt32() : 0;
                    await using var update = new NpgsqlCommand(
                        "UPDATE \"account_sets\" SET \"carried_period\" = @c, \"closed_period\" = @cl WHERE \"id\" = @id", conn, (NpgsqlTransaction)tx);
                    update.Parameters.AddWithValue("c", carried);
                    update.Parameters.AddWithValue("cl", closed);
                    update.Parameters.AddWithValue("id", accountSetId);
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                _context.ChangeTracker.Clear();

                await _operationLogService.RecordAsync(
                    accountSetId,
                    operatorUserId,
                    operatorUserName,
                    "新增",
                    "备份恢复",
                    $"从备份数据包恢复 {total} 行业务数据",
                    cancellationToken);

                return new BackupRestoreResultDto { Restored = restored, TotalRows = total, BackedUpAt = backedUpAt };
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        /// <summary>把整表导出为行字典列表。</summary>
        private static async Task<List<Dictionary<string, object?>>> DumpAsync(NpgsqlConnection conn, string table, string filterColumn, Guid filterValue, CancellationToken cancellationToken)
        {
            var rows = new List<Dictionary<string, object?>>();
            await using var cmd = new NpgsqlCommand($"SELECT * FROM \"{table}\" WHERE \"{filterColumn}\" = @id", conn);
            cmd.Parameters.AddWithValue("id", filterValue);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            return rows;
        }

        private static async Task<List<(string Name, string Type)>> GetColumnsAsync(NpgsqlConnection conn, string table, CancellationToken cancellationToken)
        {
            var columns = new List<(string Name, string Type)>();
            await using var cmd = new NpgsqlCommand(
                "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t ORDER BY ordinal_position", conn);
            cmd.Parameters.AddWithValue("t", table);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add((reader.GetString(0), reader.GetString(1)));
            }

            return columns;
        }

        /// <summary>按列类型生成显式转换（参数一律以文本传入）。</summary>
        private static string Cast(string dataType) => dataType switch
        {
            "uuid" => "::uuid",
            "date" => "::date",
            "timestamp with time zone" => "::timestamptz",
            "timestamp without time zone" => "::timestamp",
            "numeric" => "::numeric",
            "integer" => "::int",
            "bigint" => "::bigint",
            "smallint" => "::smallint",
            "boolean" => "::boolean",
            _ => "",
        };

        /// <summary>JSON 值 → 数据库参数（null 之外一律转文本，靠显式 cast 落到目标类型）。</summary>
        private static object? ToDbValue(JsonElement? element)
        {
            if (element is not { } value || value.ValueKind == JsonValueKind.Null)
            {
                return DBNull.Value;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? (object)DBNull.Value,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.GetRawText(),
            };
        }
    }
}
