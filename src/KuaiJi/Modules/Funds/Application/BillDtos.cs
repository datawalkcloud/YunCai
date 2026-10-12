using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>票据 DTO。</summary>
    public sealed class BillDto
    {
        public Guid Id { get; init; }

        public string Category { get; init; } = null!;

        public DateOnly IssueDate { get; init; }

        public string BillNo { get; init; } = null!;

        public decimal Amount { get; init; }

        public string KindText { get; init; } = null!;

        public DateOnly? DueDate { get; init; }

        public string? Drawer { get; init; }

        public string? Acceptor { get; init; }

        public string StatusText { get; init; } = null!;

        public bool Transferable { get; init; }

        public string? Remark { get; init; }

        public static string KindTextOf(BillKind kind) => kind switch
        {
            BillKind.BankAcceptance => "银行承兑汇票",
            BillKind.CommercialAcceptance => "商业承兑汇票",
            BillKind.Cheque => "支票",
            _ => "其他",
        };

        public static string StatusTextOf(BillStatus status) => status switch
        {
            BillStatus.Holding => "持有",
            BillStatus.Discounted => "已贴现",
            BillStatus.Endorsed => "已背书",
            BillStatus.Collected => "已托收",
            BillStatus.Cashed => "已兑现",
            _ => "已作废",
        };

        public static BillDto FromDomain(Bill bill) => new()
        {
            Id = bill.Id,
            Category = bill.Category,
            IssueDate = bill.IssueDate,
            BillNo = bill.BillNo,
            Amount = bill.Amount,
            KindText = KindTextOf(bill.Kind),
            DueDate = bill.DueDate,
            Drawer = bill.Drawer,
            Acceptor = bill.Acceptor,
            StatusText = StatusTextOf(bill.Status),
            Transferable = bill.Transferable,
            Remark = bill.Remark,
        };
    }

    /// <summary>新建 / 修改票据请求。</summary>
    public sealed class SaveBillRequest
    {
        public string Category { get; init; } = null!;

        public DateOnly IssueDate { get; init; }

        public string BillNo { get; init; } = null!;

        public decimal Amount { get; init; }

        /// <summary>票据种类（1 银行承兑 / 2 商业承兑 / 3 支票 / 4 其他）。</summary>
        public int Kind { get; init; }

        public DateOnly? DueDate { get; init; }

        public string? Drawer { get; init; }

        public string? Acceptor { get; init; }

        public bool Transferable { get; init; }

        public string? Remark { get; init; }
    }

    /// <summary>票据状态变更请求。</summary>
    public sealed class ChangeBillStatusRequest
    {
        /// <summary>目标状态（1 持有 / 2 已贴现 / 3 已背书 / 4 已托收 / 5 已兑现 / 6 已作废）。</summary>
        public int Status { get; init; }
    }
}
