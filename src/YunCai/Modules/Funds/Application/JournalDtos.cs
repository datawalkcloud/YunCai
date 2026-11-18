using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>日记账列表项 DTO（含期初/期末余额与收支合计，供列表展示）。</summary>
    public sealed class JournalListItemDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>账户编码（同一账套同一类型内唯一，如 001）。</summary>
        public string Code { get; init; } = null!;

        /// <summary>币别，默认人民币。</summary>
        public string Currency { get; init; } = null!;

        /// <summary>日记账类型（1=现金，2=银行存款）。</summary>
        public int Type { get; init; }

        public string TypeText { get; init; } = null!;

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        public decimal OpeningBalance { get; init; }

        public decimal ClosingBalance { get; init; }

        public decimal TotalIncome { get; init; }

        public decimal TotalExpense { get; init; }

        public int EntryCount { get; init; }

        /// <summary>是否启用。停用后不再出现在日常记账的选择列表，但保留全部流水与余额。</summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>开户银行（仅银行存款账户）。</summary>
        public string? BankName { get; init; }

        /// <summary>银行账号（仅银行存款账户）。</summary>
        public string? BankAccountNo { get; init; }

        public static JournalListItemDto FromDomain(Journal journal, Account? account) => new()
        {
            Id = journal.Id,
            Name = journal.Name,
            Code = journal.Code,
            Currency = journal.Currency,
            Type = (int)journal.Type,
            TypeText = TypeTextOf(journal.Type),
            AccountId = journal.AccountId,
            AccountCode = account?.Code ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
            OpeningBalance = journal.OpeningBalance,
            ClosingBalance = journal.ClosingBalance,
            TotalIncome = journal.TotalIncome,
            TotalExpense = journal.TotalExpense,
            EntryCount = journal.Entries.Count,
            IsEnabled = journal.IsEnabled,
            BankName = journal.BankName,
            BankAccountNo = journal.BankAccountNo,
        };

        internal static string TypeTextOf(JournalType type) => type switch
        {
            JournalType.Cash => "现金日记账",
            JournalType.BankDeposit => "银行存款日记账",
            _ => type.ToString(),
        };
    }

    /// <summary>日记账分录 DTO（含逐行余额与关联凭证号）。</summary>
    public sealed class JournalEntryDto
    {
        public Guid Id { get; init; }

        public int Sequence { get; init; }

        public DateOnly Date { get; init; }

        public string Summary { get; init; } = null!;

        public string? Category { get; init; }

        public string? Counterparty { get; init; }

        /// <summary>项目，可空。</summary>
        public string? Project { get; init; }

        /// <summary>部门，可空。</summary>
        public string? Department { get; init; }

        public decimal Income { get; init; }

        public decimal Expense { get; init; }

        public decimal Balance { get; init; }

        public Guid? VoucherId { get; init; }

        /// <summary>关联凭证号，如「记-1」；未关联时为空。</summary>
        public string? VoucherNo { get; init; }

        /// <summary>关联凭证的凭证日期；未关联时为空。用于账页「凭证日期」筛选。</summary>
        public DateOnly? VoucherDate { get; init; }

        /// <summary>关联凭证的凭证字，如「记」；未关联时为空。</summary>
        public string? VoucherWord { get; init; }

        /// <summary>关联凭证的凭证号（数字部分）；未关联时为空。</summary>
        public int? VoucherNumber { get; init; }

        /// <summary>结算方式（现金 / 转账 / 支票 等），可空。</summary>
        public string? SettlementMethod { get; init; }

        /// <summary>票据号，可空。</summary>
        public string? BillNo { get; init; }

        /// <summary>备注，可空。</summary>
        public string? Remark { get; init; }

        /// <summary>流水来源：手工登记 / Excel导入 / 内部转账 / 凭证生成；历史数据为空。</summary>
        public string? Source { get; init; }

        public static JournalEntryDto FromDomain(JournalEntry entry, Voucher? voucher) => new()
        {
            Id = entry.Id,
            Sequence = entry.Sequence,
            Date = entry.Date,
            Summary = entry.Summary,
            Category = entry.Category,
            Counterparty = entry.Counterparty,
            Project = entry.Project,
            Department = entry.Department,
            Income = entry.Income,
            Expense = entry.Expense,
            Balance = entry.Balance,
            VoucherId = entry.VoucherId,
            VoucherNo = voucher is null ? null : $"{voucher.Word}-{voucher.Number}",
            VoucherDate = voucher?.Date,
            VoucherWord = voucher?.Word,
            VoucherNumber = voucher?.Number,
            SettlementMethod = entry.SettlementMethod,
            BillNo = entry.BillNo,
            Remark = entry.Remark,
            Source = entry.Source,
        };
    }

    /// <summary>日记账详情 DTO（含全部分录）。</summary>
    public sealed class JournalDetailDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>账户编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>币别。</summary>
        public string Currency { get; init; } = null!;

        public int Type { get; init; }

        public string TypeText { get; init; } = null!;

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        public decimal OpeningBalance { get; init; }

        public decimal TotalIncome { get; init; }

        public decimal TotalExpense { get; init; }

        public decimal ClosingBalance { get; init; }

        /// <summary>是否启用。</summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>开户银行（仅银行存款账户）。</summary>
        public string? BankName { get; init; }

        /// <summary>银行账号（仅银行存款账户）。</summary>
        public string? BankAccountNo { get; init; }

        public IReadOnlyList<JournalEntryDto> Entries { get; init; } = [];

        public static JournalDetailDto FromDomain(Journal journal, Account? account, IReadOnlyDictionary<Guid, Voucher> vouchersById) => new()
        {
            Id = journal.Id,
            Name = journal.Name,
            Code = journal.Code,
            Currency = journal.Currency,
            Type = (int)journal.Type,
            TypeText = JournalListItemDto.TypeTextOf(journal.Type),
            AccountId = journal.AccountId,
            AccountCode = account?.Code ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
            OpeningBalance = journal.OpeningBalance,
            TotalIncome = journal.TotalIncome,
            TotalExpense = journal.TotalExpense,
            ClosingBalance = journal.ClosingBalance,
            IsEnabled = journal.IsEnabled,
            BankName = journal.BankName,
            BankAccountNo = journal.BankAccountNo,
            Entries = journal.Entries
                .OrderBy(e => e.Sequence)
                .Select(e => JournalEntryDto.FromDomain(
                    e,
                    e.VoucherId is { } vid ? vouchersById.GetValueOrDefault(vid) : null))
                .ToList(),
        };
    }

    /// <summary>
    /// 建账请求。指定 <see cref="AccountId"/> 时对应科目须为货币资金末级科目（库存现金 / 银行存款），且账套内该科目尚未建账；
    /// 省略时系统按类型自动在 1001 / 1002 下创建与账簿同名的明细科目并绑定。
    /// </summary>
    public sealed class CreateJournalRequest
    {
        /// <summary>日记账类型（1=现金，2=银行存款）。</summary>
        public int Type { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>账户编码；省略时由系统按类型自动取下一个可用序号。</summary>
        public string? Code { get; init; }

        /// <summary>币别；省略时默认人民币。</summary>
        public string? Currency { get; init; }

        /// <summary>对应的货币资金末级科目；省略时由系统自动创建明细科目。</summary>
        public Guid? AccountId { get; init; }

        public decimal OpeningBalance { get; init; }

        /// <summary>开户银行（仅银行存款账户，选填）。</summary>
        public string? BankName { get; init; }

        /// <summary>银行账号（仅银行存款账户，选填）。</summary>
        public string? BankAccountNo { get; init; }
    }

    /// <summary>改账请求：可修改账簿名称、期初余额与银行开户信息。</summary>
    public sealed class UpdateJournalRequest
    {
        public string Name { get; init; } = null!;

        /// <summary>账户编码；null 表示保持不变。</summary>
        public string? Code { get; init; }

        /// <summary>币别；null 表示保持不变。</summary>
        public string? Currency { get; init; }

        public decimal OpeningBalance { get; init; }

        /// <summary>启用/停用；null 表示保持不变。</summary>
        public bool? IsEnabled { get; init; }

        /// <summary>开户银行；null = 保持不变，空串 = 清除。</summary>
        public string? BankName { get; init; }

        /// <summary>银行账号；null = 保持不变，空串 = 清除。</summary>
        public string? BankAccountNo { get; init; }
    }

    /// <summary>日记账分录的新增/修改请求。</summary>
    public sealed class JournalEntryRequest
    {
        public DateOnly Date { get; init; }

        public string Summary { get; init; } = null!;

        public string? Category { get; init; }

        public string? Counterparty { get; init; }

        /// <summary>项目，可空。</summary>
        public string? Project { get; init; }

        /// <summary>部门，可空。</summary>
        public string? Department { get; init; }

        /// <summary>结算方式，可空。</summary>
        public string? SettlementMethod { get; init; }

        /// <summary>票据号，可空。</summary>
        public string? BillNo { get; init; }

        /// <summary>备注，可空。</summary>
        public string? Remark { get; init; }

        /// <summary>流水来源，可空；新增时默认「手工登记」。</summary>
        public string? Source { get; init; }

        public decimal Income { get; init; }

        public decimal Expense { get; init; }
    }

    /// <summary>
    /// 日记账分录的批量操作请求（对应「批量操作」下拉）。
    /// <see cref="Action"/> 取 delete / summary / category / counterparty / project / department / unlink-voucher；
    /// 除 delete 外只改传入的字段，因此 <see cref="Summary"/> 等为 null 表示不动。
    /// </summary>
    public sealed class BulkJournalEntryRequest
    {
        /// <summary>操作类型：delete=批量删除，summary=批量修改摘要，category=指定收支类别，counterparty=指定往来单位，project=指定项目，department=指定部门，unlink-voucher=删除关联凭证。</summary>
        public string Action { get; init; } = null!;

        /// <summary>目标分录标识集合。</summary>
        public IReadOnlyList<Guid> EntryIds { get; init; } = [];

        /// <summary>批量修改摘要时的摘要文本。</summary>
        public string? Summary { get; init; }

        /// <summary>指定收支类别时的类别；传空字符串表示清空。</summary>
        public string? Category { get; init; }

        /// <summary>指定往来单位时的往来单位；传空字符串表示清空。</summary>
        public string? Counterparty { get; init; }

        /// <summary>指定项目时的项目；传空字符串表示清空。</summary>
        public string? Project { get; init; }

        /// <summary>指定部门时的部门；传空字符串表示清空。</summary>
        public string? Department { get; init; }

        /// <summary>关联凭证时指定的凭证标识（action=link-voucher 时必填）。</summary>
        public Guid? VoucherId { get; init; }
    }

    /// <summary>由日记账分录生成凭证的请求。对方科目由前端选择，不持久化到分录。</summary>
    public sealed class GenerateVoucherRequest
    {
        /// <summary>对方科目标识（须为账套下已启用的末级科目）。</summary>
        public Guid CounterAccountId { get; init; }

        /// <summary>凭证字，省略时默认「记」。</summary>
        public string? Word { get; init; }
    }

    /// <summary>启用 / 停用资金账户请求。</summary>
    public sealed class SetJournalEnabledRequest
    {
        public bool Enabled { get; init; }
    }

    /// <summary>由多笔流水批量生成凭证的请求。</summary>
    public sealed class GenerateVouchersBatchRequest
    {
        /// <summary>勾选的流水标识（须属于同一本日记账）。</summary>
        public IReadOnlyList<Guid> EntryIds { get; init; } = [];

        /// <summary>
        /// 合并方式：1=合并成一张（默认）；2=按日期分开；3=按收支类别分开；4=按收支方向分开。
        /// 同一张凭证内相同科目、相同方向的行会自动汇总为一行。
        /// </summary>
        public int MergeBy { get; init; } = 1;

        /// <summary>统一对方科目；为空时按流水的收支类别取「默认对方科目」。</summary>
        public Guid? CounterAccountId { get; init; }

        /// <summary>凭证字，省略时默认「记」。</summary>
        public string? Word { get; init; }
    }

    /// <summary>批量生成凭证结果。</summary>
    public sealed class GenerateVouchersBatchResultDto
    {
        /// <summary>生成的凭证（按生成顺序）。</summary>
        public IReadOnlyList<BatchVoucherRefDto> Vouchers { get; init; } = [];

        /// <summary>本次涉及的流水笔数。</summary>
        public int EntryCount { get; init; }
    }

    /// <summary>批量生成凭证的单据引用。</summary>
    public sealed class BatchVoucherRefDto
    {
        public Guid Id { get; init; }

        /// <summary>显示文本（如「记-3」）。</summary>
        public string Label { get; init; } = "";

        /// <summary>该凭证包含的流水笔数。</summary>
        public int EntryCount { get; init; }
    }
}
