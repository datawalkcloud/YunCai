using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>凭证列表项 DTO（含借贷合计与分录明细，供列表卡片展示）。</summary>
    public sealed class VoucherListItemDto
    {
        public Guid Id { get; init; }

        public string Word { get; init; } = null!;

        public int Number { get; init; }

        /// <summary>凭证号显示文本，如「记-1」。</summary>
        public string VoucherNo { get; init; } = null!;

        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        /// <summary>凭证备注。</summary>
        public string? Remark { get; init; }

        public int Status { get; init; }

        public string StatusText { get; init; } = null!;

        public string Preparer { get; init; } = null!;

        public string? Reviewer { get; init; }

        public int EntryCount { get; init; }

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public bool IsBalanced { get; init; }

        public IReadOnlyList<VoucherEntryDto> Entries { get; init; } = [];

        public static VoucherListItemDto FromDomain(Voucher voucher, IReadOnlyDictionary<Guid, Account> accountsById, IReadOnlyDictionary<Guid, AssistItem> assistItemsById) => new()
        {
            Id = voucher.Id,
            Word = voucher.Word,
            Number = voucher.Number,
            VoucherNo = $"{voucher.Word}-{voucher.Number}",
            Date = voucher.Date,
            AttachmentCount = voucher.AttachmentCount,
            Remark = voucher.Remark,
            Status = (int)voucher.Status,
            StatusText = StatusTextOf(voucher.Status),
            Preparer = voucher.Preparer,
            Reviewer = voucher.Reviewer,
            EntryCount = voucher.Entries.Count,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            IsBalanced = voucher.IsBalanced,
            Entries = voucher.Entries
                .OrderBy(e => e.LineNumber)
                .Select(e => VoucherEntryDto.FromDomain(e, accountsById.GetValueOrDefault(e.AccountId), assistItemsById))
                .ToList(),
        };

        internal static string StatusTextOf(VoucherStatus status) => status switch
        {
            VoucherStatus.Draft => "暂存",
            VoucherStatus.Posted => "正常",
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

        /// <summary>该分录登记的辅助核算取值（未启用辅助核算时为空集合）。</summary>
        public IReadOnlyList<VoucherEntryAssistDto> Assists { get; init; } = [];

        public static VoucherEntryDto FromDomain(VoucherEntry entry, Account? account, IReadOnlyDictionary<Guid, AssistItem> assistItemsById) => new()
        {
            Id = entry.Id,
            LineNumber = entry.LineNumber,
            Summary = entry.Summary,
            AccountId = entry.AccountId,
            AccountCode = account?.Code ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
            Debit = entry.Debit,
            Credit = entry.Credit,
            Assists = entry.Assists
                .Select(a => VoucherEntryAssistDto.FromDomain(a, assistItemsById.GetValueOrDefault(a.AssistItemId)))
                .ToList(),
        };
    }

    /// <summary>凭证分录的一条辅助核算取值（响应，含档案编码与名称，供凭证详情与打印展示）。</summary>
    public sealed class VoucherEntryAssistDto
    {
        /// <summary>辅助核算类别。</summary>
        public AssistType Type { get; init; }

        /// <summary>类别中文名。</summary>
        public string TypeText { get; init; } = null!;

        /// <summary>档案标识。</summary>
        public Guid AssistItemId { get; init; }

        /// <summary>档案编码；档案已被删除时为空串。</summary>
        public string ItemCode { get; init; } = null!;

        /// <summary>档案名称；档案已被删除时为占位文案。</summary>
        public string ItemName { get; init; } = null!;

        public static VoucherEntryAssistDto FromDomain(VoucherEntryAssist assist, AssistItem? item) => new()
        {
            Type = assist.Type,
            TypeText = AssistTypeText.Of(assist.Type),
            AssistItemId = assist.AssistItemId,
            ItemCode = item?.Code ?? string.Empty,
            ItemName = item?.Name ?? "（档案已删除）",
        };
    }

    /// <summary>
    /// 新建/编辑凭证时一条分录上的辅助核算取值请求。
    /// 显式定义请求形状而不直接暴露领域的 <see cref="AssistRef"/>（只读结构体），
    /// 以免 OpenAPI 生成出无法控制的序列化形式。
    /// </summary>
    public sealed class VoucherEntryAssistRequest
    {
        /// <summary>辅助核算类别（须为该科目已设置的维度）。</summary>
        public AssistType Type { get; init; }

        /// <summary>档案标识（须为该账套下该类别的末级已启用档案）。</summary>
        public Guid ItemId { get; init; }

        /// <summary>转换为领域引用。</summary>
        public AssistRef ToReference() => new(Type, ItemId);
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

        /// <summary>凭证备注。</summary>
        public string? Remark { get; init; }

        public int Status { get; init; }

        public string StatusText { get; init; } = null!;

        public string Preparer { get; init; } = null!;

        public string? Reviewer { get; init; }

        public decimal TotalDebit { get; init; }

        public decimal TotalCredit { get; init; }

        public bool IsBalanced { get; init; }

        public IReadOnlyList<VoucherEntryDto> Entries { get; init; } = [];

        public static VoucherDetailDto FromDomain(Voucher voucher, IReadOnlyDictionary<Guid, Account> accountsById, IReadOnlyDictionary<Guid, AssistItem> assistItemsById) => new()
        {
            Id = voucher.Id,
            Word = voucher.Word,
            Number = voucher.Number,
            VoucherNo = $"{voucher.Word}-{voucher.Number}",
            Date = voucher.Date,
            AttachmentCount = voucher.AttachmentCount,
            Remark = voucher.Remark,
            Status = (int)voucher.Status,
            StatusText = VoucherListItemDto.StatusTextOf(voucher.Status),
            Preparer = voucher.Preparer,
            Reviewer = voucher.Reviewer,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            IsBalanced = voucher.IsBalanced,
            Entries = voucher.Entries
                .OrderBy(e => e.LineNumber)
                .Select(e => VoucherEntryDto.FromDomain(e, accountsById.GetValueOrDefault(e.AccountId), assistItemsById))
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

        /// <summary>该分录挂载的辅助核算取值；为空表示不挂任何维度。</summary>
        public IReadOnlyList<VoucherEntryAssistRequest> Assists { get; init; } = [];
    }

    /// <summary>新建凭证请求。凭证号为 0（或省略）时由后端按凭证字自动续号。</summary>
    public sealed class CreateVoucherRequest
    {
        public string Word { get; init; } = "记";

        public int Number { get; init; }

        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        /// <summary>凭证备注，可省略。</summary>
        public string? Remark { get; init; }

        public IReadOnlyList<CreateVoucherEntryRequest> Entries { get; init; } = [];
    }

    /// <summary>导出凭证请求：给 ids 则只导出选中凭证，否则按 Query 的条件导出。</summary>
    public sealed class ExportVouchersRequest
    {
        /// <summary>选中的凭证明细标识；为空表示按条件导出。</summary>
        public IReadOnlyList<Guid>? Ids { get; init; }

        /// <summary>导出范围条件，省略时导出全部未删除凭证。</summary>
        public VoucherQuery? Query { get; init; }
    }

    /// <summary>凭证 PDF 打印请求：选中凭证 + 打印版式选项。</summary>
    public sealed class VoucherPdfRequest
    {
        /// <summary>选中的凭证标识；为空表示按条件打印。</summary>
        public IReadOnlyList<Guid>? Ids { get; init; }

        /// <summary>打印范围条件，省略时打印全部未删除凭证。</summary>
        public VoucherQuery? Query { get; init; }

        /* —— 版式选项，与前端「凭证打印」设置同构 —— */
        public string Paper { get; init; } = "A4x2";
        public string Orientation { get; init; } = "portrait";
        public string Valign { get; init; } = "top";
        public int Rows { get; init; } = 5;
        public int FontSize { get; init; } = 10;
        public int MarginLeft { get; init; } = 25;
        public int MarginRight { get; init; } = 10;
        public int MarginTop { get; init; } = 14;
        public int MarginBottom { get; init; } = 15;
        public bool SignPreparer { get; init; } = true;
        public string? PreparerText { get; init; }
        public bool SignReviewer { get; init; } = true;
        public string? ReviewerText { get; init; }
        public bool SignManager { get; init; }
        public string? ManagerText { get; init; }
        public bool SignBookkeeper { get; init; }
        public string? BookkeeperText { get; init; }
        public bool SignCashier { get; init; }
        public string? CashierText { get; init; }
        public bool SignTime { get; init; }
        public bool ShowAccountCode { get; init; } = true;
        public bool ZeroAttachment { get; init; }
        public bool MergePrint { get; init; }
    }

    /// <summary>整理凭证请求：期间 + 凭证字 + 重排依据。</summary>
    public sealed class RenumberVouchersRequest
    {
        /// <summary>凭证字；为空表示全部凭证字。</summary>
        public string? Word { get; init; }

        /// <summary>整理期间起（含）；为空表示不限。</summary>
        public DateOnly? PeriodStart { get; init; }

        /// <summary>整理期间止（含）；为空表示不限。</summary>
        public DateOnly? PeriodEnd { get; init; }

        /// <summary>true 按凭证日期重排，false 按凭证号重排。</summary>
        public bool ByDate { get; init; }
    }

    /// <summary>编辑凭证请求。凭证字与号不可更改，仅可修改日期、附单据数与全部分录（整体替换）。</summary>
    public sealed class UpdateVoucherRequest
    {
        public DateOnly Date { get; init; }

        public int AttachmentCount { get; init; }

        /// <summary>凭证备注，可省略。</summary>
        public string? Remark { get; init; }

        public IReadOnlyList<CreateVoucherEntryRequest> Entries { get; init; } = [];
    }
}
