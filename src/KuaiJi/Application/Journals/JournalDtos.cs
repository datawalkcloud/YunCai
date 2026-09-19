using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Journals;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Journals
{
    /// <summary>日记账列表项 DTO（含期初/期末余额与收支合计，供列表展示）。</summary>
    public sealed class JournalListItemDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

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

        public static JournalListItemDto FromDomain(Journal journal, Account? account) => new()
        {
            Id = journal.Id,
            Name = journal.Name,
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

        public decimal Income { get; init; }

        public decimal Expense { get; init; }

        public decimal Balance { get; init; }

        public Guid? VoucherId { get; init; }

        /// <summary>关联凭证号，如「记-1」；未关联时为空。</summary>
        public string? VoucherNo { get; init; }

        public static JournalEntryDto FromDomain(JournalEntry entry, Voucher? voucher) => new()
        {
            Id = entry.Id,
            Sequence = entry.Sequence,
            Date = entry.Date,
            Summary = entry.Summary,
            Category = entry.Category,
            Counterparty = entry.Counterparty,
            Income = entry.Income,
            Expense = entry.Expense,
            Balance = entry.Balance,
            VoucherId = entry.VoucherId,
            VoucherNo = voucher is null ? null : $"{voucher.Word}-{voucher.Number}",
        };
    }

    /// <summary>日记账详情 DTO（含全部分录）。</summary>
    public sealed class JournalDetailDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        public int Type { get; init; }

        public string TypeText { get; init; } = null!;

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        public decimal OpeningBalance { get; init; }

        public decimal TotalIncome { get; init; }

        public decimal TotalExpense { get; init; }

        public decimal ClosingBalance { get; init; }

        public IReadOnlyList<JournalEntryDto> Entries { get; init; } = [];

        public static JournalDetailDto FromDomain(Journal journal, Account? account, IReadOnlyDictionary<Guid, Voucher> vouchersById) => new()
        {
            Id = journal.Id,
            Name = journal.Name,
            Type = (int)journal.Type,
            TypeText = JournalListItemDto.TypeTextOf(journal.Type),
            AccountId = journal.AccountId,
            AccountCode = account?.Code ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
            OpeningBalance = journal.OpeningBalance,
            TotalIncome = journal.TotalIncome,
            TotalExpense = journal.TotalExpense,
            ClosingBalance = journal.ClosingBalance,
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

        /// <summary>对应的货币资金末级科目；省略时由系统自动创建明细科目。</summary>
        public Guid? AccountId { get; init; }

        public decimal OpeningBalance { get; init; }
    }

    /// <summary>改账请求：可修改账簿名称与期初余额。</summary>
    public sealed class UpdateJournalRequest
    {
        public string Name { get; init; } = null!;

        public decimal OpeningBalance { get; init; }
    }

    /// <summary>日记账分录的新增/修改请求。</summary>
    public sealed class JournalEntryRequest
    {
        public DateOnly Date { get; init; }

        public string Summary { get; init; } = null!;

        public string? Category { get; init; }

        public string? Counterparty { get; init; }

        public decimal Income { get; init; }

        public decimal Expense { get; init; }
    }

    /// <summary>由日记账分录生成凭证的请求。对方科目由前端选择，不持久化到分录。</summary>
    public sealed class GenerateVoucherRequest
    {
        /// <summary>对方科目标识（须为账套下已启用的末级科目）。</summary>
        public Guid CounterAccountId { get; init; }

        /// <summary>凭证字，省略时默认「记」。</summary>
        public string? Word { get; init; }
    }
}
