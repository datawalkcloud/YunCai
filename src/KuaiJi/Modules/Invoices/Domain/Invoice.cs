namespace KuaiJi.Modules.Invoices.Domain
{
    /// <summary>发票方向。</summary>
    public enum InvoiceDirection
    {
        /// <summary>销项发票。</summary>
        Sales = 1,

        /// <summary>进项发票。</summary>
        Purchase = 2,
    }

    /// <summary>发票入账状态。</summary>
    public enum InvoiceStatus
    {
        /// <summary>未入账。</summary>
        Pending = 1,

        /// <summary>已入账。</summary>
        Posted = 2,

        /// <summary>暂不入账。</summary>
        Suspended = 3,
    }

    /// <summary>
    /// 发票聚合根：进销项发票池。一张发票包含若干商品明细行，
    /// 金额 / 税额 / 价税合计由行汇总；进项发票可认证（认证日期）并入账，
    /// 销项发票用于与资金收款核对（发票资金一览表）。
    /// </summary>
    public class Invoice
    {
        /// <summary>发票号最大长度。</summary>
        public const int MaxInvoiceNoLength = 50;

        /// <summary>购 / 销方名称最大长度。</summary>
        public const int MaxPartnerLength = 200;

        private readonly List<InvoiceLine> _lines = new();

        private Invoice()
        {
        }

        private Invoice(
            Guid id,
            Guid accountSetId,
            InvoiceDirection direction,
            string invoiceType,
            DateOnly inputDate,
            DateOnly issueDate,
            string invoiceNo,
            string partnerName,
            string? bizType,
            string? source,
            string? remark)
        {
            Id = id;
            AccountSetId = accountSetId;
            Direction = direction;
            InvoiceType = invoiceType;
            InputDate = inputDate;
            IssueDate = issueDate;
            InvoiceNo = invoiceNo;
            PartnerName = partnerName;
            BizType = bizType;
            Source = source;
            Remark = remark;
            Status = InvoiceStatus.Pending;
        }

        /// <summary>发票标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>发票方向（销项 / 进项）。</summary>
        public InvoiceDirection Direction { get; private set; }

        /// <summary>发票种类（增值税专用发票 / 增值税普通发票 / 数电专票…）。</summary>
        public string InvoiceType { get; private set; } = null!;

        /// <summary>录入日期。</summary>
        public DateOnly InputDate { get; private set; }

        /// <summary>开票日期。</summary>
        public DateOnly IssueDate { get; private set; }

        /// <summary>发票号码，同一账套同一方向内唯一。</summary>
        public string InvoiceNo { get; private set; } = null!;

        /// <summary>客户名称（销项）/ 供应商名称（进项）。</summary>
        public string PartnerName { get; private set; } = null!;

        /// <summary>业务类型，可为空。</summary>
        public string? BizType { get; private set; }

        /// <summary>发票来源（手工新增 / 一键取票…），可为空。</summary>
        public string? Source { get; private set; }

        /// <summary>备注。</summary>
        public string? Remark { get; private set; }

        /// <summary>入账状态。</summary>
        public InvoiceStatus Status { get; private set; }

        /// <summary>认证日期（仅进项发票），可为空。</summary>
        public DateOnly? AuthDate { get; private set; }

        /// <summary>税款所属期（仅进项发票，yyyyMM），可为空。</summary>
        public int? TaxPeriod { get; private set; }

        /// <summary>生成凭证标识（入账生成凭证后回填）。</summary>
        public Guid? VoucherId { get; private set; }

        /// <summary>商品明细行集合。</summary>
        public IReadOnlyList<InvoiceLine> Lines => _lines;

        /// <summary>合计金额（不含税）。</summary>
        public decimal TotalAmount => _lines.Sum(l => l.Amount);

        /// <summary>合计税额。</summary>
        public decimal TotalTax => _lines.Sum(l => l.TaxAmount);

        /// <summary>价税合计。</summary>
        public decimal TotalWithTax => TotalAmount + TotalTax;

        /// <summary>新建发票，须归属于某一账套；至少一行商品明细。</summary>
        public static Invoice Create(
            Guid accountSetId,
            InvoiceDirection direction,
            string invoiceType,
            DateOnly inputDate,
            DateOnly issueDate,
            string invoiceNo,
            string partnerName,
            string? bizType,
            string? source,
            string? remark)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            GuardText(invoiceType, "发票种类", 50);
            GuardText(invoiceNo, "发票号码", MaxInvoiceNoLength);
            GuardText(partnerName, "购 / 销方名称", MaxPartnerLength);
            return new Invoice(
                Guid.NewGuid(),
                accountSetId,
                direction,
                invoiceType.Trim(),
                inputDate,
                issueDate,
                invoiceNo.Trim(),
                partnerName.Trim(),
                Normalize(bizType),
                Normalize(source),
                Normalize(remark));
        }

        /// <summary>修改发票头信息（不含状态）。</summary>
        public void Update(
            string invoiceType,
            DateOnly inputDate,
            DateOnly issueDate,
            string invoiceNo,
            string partnerName,
            string? bizType,
            string? remark)
        {
            GuardText(invoiceType, "发票种类", 50);
            GuardText(invoiceNo, "发票号码", MaxInvoiceNoLength);
            GuardText(partnerName, "购 / 销方名称", MaxPartnerLength);
            InvoiceType = invoiceType.Trim();
            InputDate = inputDate;
            IssueDate = issueDate;
            InvoiceNo = invoiceNo.Trim();
            PartnerName = partnerName.Trim();
            BizType = Normalize(bizType);
            Remark = Normalize(remark);
        }

        /// <summary>
        /// 整体替换商品明细行。行金额必须非零（红冲发票由税局取票导入，允许负数），税率 0-100。
        /// </summary>
        public void ReplaceLines(
            IReadOnlyList<(string goodsName, decimal amount, decimal taxRate, decimal taxAmount)> lines,
            bool allowNegative = false)
        {
            if (lines.Count == 0)
            {
                throw new ArgumentException("发票至少要有一行商品明细。");
            }

            var result = new List<InvoiceLine>();
            for (var i = 0; i < lines.Count; i++)
            {
                var (goodsName, amount, taxRate, taxAmount) = lines[i];
                if (string.IsNullOrWhiteSpace(goodsName))
                {
                    throw new ArgumentException($"第 {i + 1} 行商品名称不能为空。");
                }

                if (amount == 0m || (!allowNegative && amount < 0m))
                {
                    throw new ArgumentException($"第 {i + 1} 行金额必须大于零。");
                }

                if (taxRate < 0m || taxRate > 100m)
                {
                    throw new ArgumentException($"第 {i + 1} 行税率必须在 0-100 之间。");
                }

                if (taxAmount < 0m && !allowNegative)
                {
                    throw new ArgumentException($"第 {i + 1} 行税额不能为负数。");
                }

                result.Add(new InvoiceLine(Guid.NewGuid(), Id, i + 1, goodsName.Trim(), amount, taxRate, taxAmount));
            }

            _lines.Clear();
            _lines.AddRange(result);
        }

        /// <summary>进项发票认证（登记认证日期与税款所属期）。销项发票不可认证。</summary>
        public void Authenticate(DateOnly authDate, int? taxPeriod)
        {
            if (Direction != InvoiceDirection.Purchase)
            {
                throw new InvalidOperationException("仅进项发票可以认证。");
            }

            AuthDate = authDate;
            TaxPeriod = taxPeriod;
        }

        /// <summary>变更入账状态。</summary>
        public void ChangeStatus(InvoiceStatus status)
        {
            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("发票状态无效。");
            }

            Status = status;
        }

        /// <summary>回填生成凭证标识。</summary>
        public void AttachVoucher(Guid voucherId)
        {
            VoucherId = voucherId == Guid.Empty ? null : voucherId;
        }

        /// <summary>
        /// 自愈：清除指向已删除凭证的残留引用。
        /// 凭证被删除（进回收站或彻底删除）后，发票仍保留 VoucherId 会同时造成两个后果：
        /// 「关联凭证」列显示空白，且该发票因「已入账」被永久排除、无法再次生成凭证。
        /// 清引用时一并回退为未入账，使其重新回到入账流程。返回是否发生了变更。
        /// </summary>
        public bool ClearStaleVoucher(IReadOnlySet<Guid> existingVoucherIds)
        {
            if (VoucherId is not { } id || existingVoucherIds.Contains(id))
            {
                return false;
            }

            VoucherId = null;
            if (Status == InvoiceStatus.Posted)
            {
                Status = InvoiceStatus.Pending;
            }

            return true;
        }

        private static void GuardText(string value, string field, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{field}不能为空。");
            }

            if (value.Trim().Length > maxLength)
            {
                throw new ArgumentException($"{field}不能超过 {maxLength} 个字符。");
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>发票商品明细行。</summary>
    public class InvoiceLine
    {
        private InvoiceLine()
        {
        }

        internal InvoiceLine(Guid id, Guid invoiceId, int lineNumber, string goodsName, decimal amount, decimal taxRate, decimal taxAmount)
        {
            Id = id;
            InvoiceId = invoiceId;
            LineNumber = lineNumber;
            GoodsName = goodsName;
            Amount = amount;
            TaxRate = taxRate;
            TaxAmount = taxAmount;
        }

        /// <summary>明细行标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属发票标识。</summary>
        public Guid InvoiceId { get; private set; }

        /// <summary>行号（从 1 开始）。</summary>
        public int LineNumber { get; private set; }

        /// <summary>商品名称。</summary>
        public string GoodsName { get; private set; } = null!;

        /// <summary>金额（不含税）。</summary>
        public decimal Amount { get; private set; }

        /// <summary>税率（百分比）。</summary>
        public decimal TaxRate { get; private set; }

        /// <summary>税额。</summary>
        public decimal TaxAmount { get; private set; }
    }
}
