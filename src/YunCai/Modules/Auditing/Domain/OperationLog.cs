namespace YunCai.Modules.Auditing.Domain
{
    /// <summary>
    /// 操作日志：账套内关键操作的审计留痕（谁、何时、在哪个模块、做了什么）。
    /// 只增不改不删，由各应用服务在完成业务动作后写入。
    /// </summary>
    public class OperationLog
    {
        /// <summary>日志正文最大长度。</summary>
        public const int MaxLogLength = 500;

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
            DateTime time)
        {
            Id = id;
            AccountSetId = accountSetId;
            UserId = userId;
            UserName = userName;
            Type = type;
            Module = module;
            Log = log;
            Time = time;
        }

        /// <summary>日志标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>操作用户标识。</summary>
        public string UserId { get; private set; } = null!;

        /// <summary>操作用户姓名。</summary>
        public string UserName { get; private set; } = null!;

        /// <summary>日志类型（操作 / 登录 / 删除…）。</summary>
        public string Type { get; private set; } = null!;

        /// <summary>模块（凭证 / 资金 / 期末结转…）。</summary>
        public string Module { get; private set; } = null!;

        /// <summary>日志正文。</summary>
        public string Log { get; private set; } = null!;

        /// <summary>操作时间。</summary>
        public DateTime Time { get; private set; }

        /// <summary>写入一条操作日志。</summary>
        public static OperationLog Create(
            Guid accountSetId,
            string userId,
            string userName,
            string type,
            string module,
            string log)
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
                DateTime.UtcNow);
        }
    }
}
