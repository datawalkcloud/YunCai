using YunCai.Modules.Funds.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>票据 DTO。</summary>
    public sealed class BillDto
    {
        public Guid Id { get; init; }

        public string Category { get; init; } = string.Empty;

        public DateOnly IssueDate { get; init; }

        public string BillNo { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        /// <summary>票据种类（1 银行汇票 / 2 商业汇票 / 3 支票 / 4 其他）。</summary>
        public int Kind { get; init; }

        public string KindText { get; init; } = string.Empty;

        public DateOnly? DueDate { get; init; }

        public string? Drawer { get; init; }

        public string? Payee { get; init; }

        public string? Acceptor { get; init; }

        public string? DrawerAccountNo { get; init; }

        public string? PayeeAccountNo { get; init; }

        public string? AcceptorAccountNo { get; init; }

        public string? DrawerBank { get; init; }

        public string? PayeeBank { get; init; }

        public string? AcceptorBank { get; init; }

        /// <summary>票据状态（1 提示收票已签收 / 2 买断式贴现已签收 / 3 背书已签收 / 5 票据已结清 / 7 背书待签收）。</summary>
        public int Status { get; init; }

        public string StatusText { get; init; } = string.Empty;

        public bool Transferable { get; init; }

        public string? Remark { get; init; }

        /// <summary>已生成凭证标识。</summary>
        public Guid? VoucherId { get; init; }

        /// <summary>已生成凭证号（如「记-8」）。</summary>
        public string? VoucherNo { get; init; }

        /// <summary>附件数量。</summary>
        public int AttachmentCount { get; init; }

        public static string KindTextOf(BillKind kind) => kind switch
        {
            BillKind.BankAcceptance => "银行汇票",
            BillKind.CommercialAcceptance => "商业汇票",
            BillKind.Cheque => "支票",
            _ => "其他",
        };

        public static string StatusTextOf(BillStatus status) => status switch
        {
            BillStatus.Holding => "提示收票已签收",
            BillStatus.Endorsing => "背书待签收",
            BillStatus.Endorsed => "背书已签收",
            BillStatus.Discounted => "买断式贴现已签收",
            BillStatus.Cashed => "票据已结清",
            BillStatus.Collected => "已托收",
            _ => "已作废",
        };

        public static BillDto FromDomain(Bill bill, int attachmentCount = 0, string? voucherNo = null) => new()
        {
            Id = bill.Id,
            Category = bill.Category,
            IssueDate = bill.IssueDate,
            BillNo = bill.BillNo,
            Amount = bill.Amount,
            Kind = (int)bill.Kind,
            KindText = KindTextOf(bill.Kind),
            DueDate = bill.DueDate,
            Drawer = bill.Drawer,
            Payee = bill.Payee,
            Acceptor = bill.Acceptor,
            DrawerAccountNo = bill.DrawerAccountNo,
            PayeeAccountNo = bill.PayeeAccountNo,
            AcceptorAccountNo = bill.AcceptorAccountNo,
            DrawerBank = bill.DrawerBank,
            PayeeBank = bill.PayeeBank,
            AcceptorBank = bill.AcceptorBank,
            Status = (int)bill.Status,
            StatusText = StatusTextOf(bill.Status),
            Transferable = bill.Transferable,
            Remark = bill.Remark,
            VoucherId = bill.VoucherId,
            VoucherNo = voucherNo,
            AttachmentCount = attachmentCount,
        };
    }

    /// <summary>新建 / 修改票据请求。</summary>
    public sealed class SaveBillRequest
    {
        /// <summary>收支类别（可空，留空则生成凭证时跳过）。</summary>
        public string? Category { get; init; }

        public DateOnly IssueDate { get; init; }

        public string BillNo { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        /// <summary>票据种类（1 银行汇票 / 2 商业汇票 / 3 支票 / 4 其他）。</summary>
        public int Kind { get; init; } = 1;

        public DateOnly? DueDate { get; init; }

        public string? Drawer { get; init; }

        public string? Payee { get; init; }

        public string? Acceptor { get; init; }

        public string? DrawerAccountNo { get; init; }

        public string? PayeeAccountNo { get; init; }

        public string? AcceptorAccountNo { get; init; }

        public string? DrawerBank { get; init; }

        public string? PayeeBank { get; init; }

        public string? AcceptorBank { get; init; }

        public bool Transferable { get; init; } = true;

        /// <summary>票据状态（新建可指定，为空按「提示收票已签收」）。</summary>
        public int? Status { get; init; }

        public string? Remark { get; init; }
    }

    /// <summary>票据状态变更请求。</summary>
    public sealed class ChangeBillStatusRequest
    {
        /// <summary>目标状态（1 提示收票已签收 / 2 买断式贴现已签收 / 3 背书已签收 / 5 票据已结清 / 6 已作废 / 7 背书待签收）。</summary>
        public int Status { get; init; }
    }

    /// <summary>票据批量操作请求（删除 / 生成凭证）。</summary>
    public sealed class BulkBillRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        /// <summary>生成凭证方式：collect（收款凭证）/ discount（贴现凭证）；为空按收款凭证处理。</summary>
        public string? VoucherKind { get; init; }
    }

    /// <summary>批量生成凭证结果。</summary>
    public sealed class BulkBillVoucherResult
    {
        /// <summary>成功生成张数。</summary>
        public int Created { get; init; }

        /// <summary>被跳过条数（已生成凭证 / 状态不符 / 无收支类别）。</summary>
        public int Skipped { get; init; }
    }

    /// <summary>票据导入结果。</summary>
    public sealed class BillImportResult
    {
        public int TotalRows { get; init; }

        public int Imported { get; init; }

        public int Skipped { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
}
