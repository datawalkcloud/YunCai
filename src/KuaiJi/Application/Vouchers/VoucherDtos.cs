using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Vouchers
{
    /// <summary>凭证列表项 DTO（含借贷合计，供列表展示）。</summary>
    public sealed class VoucherListItemDto
    {
        public Guid Id { get; init; }

        public string Word { get; init; } = null!;

        public int Number { get; init; }

        /// <summary>凭证号显示文本，如「记-1」。</summary>
        public string VoucherNo { get; init; } = null!;

        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        public int Status { get; init; }

        public string StatusText { get; init; } = null!;

        public string Preparer { get; init; } = null!;

        public string? Reviewer { get; init; }

        public int EntryCount { get; init; }

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public bool IsBalanced { get; init; }

        public static VoucherListItemDto FromDomain(Voucher voucher) => new()
        {
            Id = voucher.Id,
            Word = voucher.Word,
            Number = voucher.Number,
            VoucherNo = $"{voucher.Word}-{voucher.Number}",
            Date = voucher.Date,
            AttachmentCount = voucher.AttachmentCount,
            Status = (int)voucher.Status,
            StatusText = StatusTextOf(voucher.Status),
            Preparer = voucher.Preparer,
            Reviewer = voucher.Reviewer,
            EntryCount = voucher.Entries.Count,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            IsBalanced = voucher.IsBalanced,
        };

        internal static string StatusTextOf(VoucherStatus status) => status switch
        {
            VoucherStatus.Draft => "草稿",
            VoucherStatus.Reviewed => "已审核",
            VoucherStatus.Posted => "已过账",
            _ => status.ToString(),
        };
    }

    /// <summary>凭证分录 DTO（含科目编码与名称，便于前端展示）。</summary>
    public sealed class VoucherEntryDto
    {
        public Guid Id { get; init; }

        public int LineNumber { get; init; }

        public string Summary { get; init; } = null!;

        public Guid AccountId { get; init; }

        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        public decimal Debit { get; init; }

        public decimal Credit { get; init; }

        public static VoucherEntryDto FromDomain(VoucherEntry entry, Account? account) => new()
        {
            Id = entry.Id,
            LineNumber = entry.LineNumber,
            Summary = entry.Summary,
            AccountId = entry.AccountId,
            AccountCode = account?.Code ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
            Debit = entry.Debit,
            Credit = entry.Credit,
        };
    }

    /// <summary>凭证详情 DTO（含全部分录）。</summary>
    public sealed class VoucherDetailDto
    {
        public Guid Id { get; init; }

        public string Word { get; init; } = null!;

        public int Number { get; init; }

        public string VoucherNo { get; init; } = null!;

        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        public int Status { get; init; }

        public string StatusText { get; init; } = null!;

        public string Preparer { get; init; } = null!;

        public string? Reviewer { get; init; }

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public bool IsBalanced { get; init; }

        public IReadOnlyList<VoucherEntryDto> Entries { get; init; } = [];

        public static VoucherDetailDto FromDomain(Voucher voucher, IReadOnlyDictionary<Guid, Account> accountsById) => new()
        {
            Id = voucher.Id,
            Word = voucher.Word,
            Number = voucher.Number,
            VoucherNo = $"{voucher.Word}-{voucher.Number}",
            Date = voucher.Date,
            AttachmentCount = voucher.AttachmentCount,
            Status = (int)voucher.Status,
            StatusText = VoucherListItemDto.StatusTextOf(voucher.Status),
            Preparer = voucher.Preparer,
            Reviewer = voucher.Reviewer,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            IsBalanced = voucher.IsBalanced,
            Entries = voucher.Entries
                .OrderBy(e => e.LineNumber)
                .Select(e => VoucherEntryDto.FromDomain(e, accountsById.GetValueOrDefault(e.AccountId)))
                .ToList(),
        };
    }

    /// <summary>新建凭证的一条分录请求（借贷单边非零）。</summary>
    public sealed class CreateVoucherEntryRequest
    {
        public string Summary { get; init; } = null!;

        public Guid AccountId { get; init; }

        public decimal Debit { get; init; }

        public decimal Credit { get; init; }
    }

    /// <summary>新建凭证请求。凭证号为 0（或省略）时由后端按凭证字自动续号。</summary>
    public sealed class CreateVoucherRequest
    {
        public string Word { get; init; } = "记";

        public int Number { get; init; }

        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        public IReadOnlyList<CreateVoucherEntryRequest> Entries { get; init; } = [];
    }

    /// <summary>编辑凭证请求。凭证字与号不可更改，仅可修改日期、附单据数与全部分录（整体替换）。</summary>
    public sealed class UpdateVoucherRequest
    {
        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        public IReadOnlyList<CreateVoucherEntryRequest> Entries { get; init; } = [];
    }
}
