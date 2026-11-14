namespace YunCai.Modules.Auditing.Domain
{
    /// <summary>
    /// 操作日志：账套内关键操作的审计留痕（谁、何时、在哪个模块、对哪条记录、改前改后）。
    /// 只增不改不删；业务动作的摘要由控制器写入，实体级变更由 SaveChanges 拦截器自动补记。
    /// </summary>
    public class OperationLog
    {
        /// <summary>日志正文最大长度。</summary>
        public const int MaxLogLength = 500;

        /// <summary>改前 / 改后快照最大长度（超出截断，避免大字段撑爆审计表）。</summary>
        public const int MaxSnapshotLength = 4000;

        private OperationLog()
        {
        }

        public OperationLog(
            Guid id,
            Guid accountSetId,
            string userId,
            string userName,
            string type,
            string module,
            string log,
            DateTime time,
            string? entityType = null,
            string? entityId = null,
            string? beforeJson = null,
            string? afterJson = null,
            string? ipAddress = null)
        {
            Id = id;
            AccountSetId = accountSetId;
            UserId = userId;
            UserName = userName;
            Type = type;
            Module = module;
            Log = log;
            Time = time;
            EntityType = entityType;
            EntityId = entityId;
            BeforeJson = beforeJson;
            AfterJson = afterJson;
            IpAddress = ipAddress;
        }

        /// <summary>日志标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>操作用户标识。</summary>
        public string UserId { get; private set; } = null!;

        /// <summary>操作用户姓名。</summary>
        public string UserName { get; private set; } = null!;

        /// <summary>日志类型（操作 / 数据变更 / 删除…）。</summary>
        public string Type { get; private set; } = null!;

        /// <summary>模块（凭证 / 资金 / 期末结转…）。</summary>
        public string Module { get; private set; } = null!;

        /// <summary>日志正文。</summary>
        public string Log { get; private set; } = null!;

        /// <summary>操作时间。</summary>
        public DateTime Time { get; private set; }

        /// <summary>被操作实体类型（如 Voucher、AccountSetMember）。</summary>
        public string? EntityType { get; private set; }

        /// <summary>被操作实体主键。</summary>
        public string? EntityId { get; private set; }

        /// <summary>变更前快照（JSON，已脱敏）。</summary>
        public string? BeforeJson { get; private set; }

        /// <summary>变更后快照（JSON，已脱敏）。</summary>
        public string? AfterJson { get; private set; }

        /// <summary>来源 IP。</summary>
        public string? IpAddress { get; private set; }

        /// <summary>写入一条操作日志。</summary>
        public static OperationLog Create(
            Guid accountSetId,
            string userId,
            string userName,
            string type,
            string module,
            string log,
            string? entityType = null,
            string? entityId = null,
            string? beforeJson = null,
            string? afterJson = null,
            string? ipAddress = null)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            var text = (log ?? string.Empty).Trim();
            if (text.Length > MaxLogLength)
            {
                text = text[..MaxLogLength];
            }

            return new OperationLog(
                Guid.NewGuid(),
                accountSetId,
                userId?.Trim() ?? string.Empty,
                userName?.Trim() ?? string.Empty,
                type?.Trim() ?? "操作",
                module?.Trim() ?? string.Empty,
                text,
                DateTime.UtcNow,
                Trim(entityType, 50),
                Trim(entityId, 64),
                Snapshot(beforeJson),
                Snapshot(afterJson),
                Trim(ipAddress, 45));
        }

        private static string? Trim(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = value.Trim();
            return text.Length > max ? text[..max] : text;
        }

        private static string? Snapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return json.Length > MaxSnapshotLength ? json[..MaxSnapshotLength] : json;
        }
    }
}
