using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Invoices.Domain;
using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Invoices.Application
{
    /// <summary>发票相关业务错误。</summary>
    public sealed class InvoiceException : Exception
    {
        public InvoiceException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 发票应用服务：进销项发票池的查询 / 新建 / 修改 / 删除 / 认证 / 入账状态流转，
    /// 以及「发票资金一览表」（销项开票与资金收款的往来单位核对）。
    /// </summary>
    public sealed class InvoiceService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IJournalRepository _journalRepository;

        public InvoiceService(
            IAccountSetRepository accountSetRepository,
            IInvoiceRepository invoiceRepository,
            IJournalRepository journalRepository)
        {
            _accountSetRepository = accountSetRepository;
            _invoiceRepository = invoiceRepository;
            _journalRepository = journalRepository;
        }

        /// <summary>按方向与日期区间查询发票列表（含明细行）。</summary>
        public async Task<IReadOnlyList<InvoiceDto>> GetRangeAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            InvoiceStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var invoices = await _invoiceRepository.GetRangeAsync(accountSetId, direction, startDate, endDate, status, cancellationToken);
            return invoices.Select(ToDto).ToList();
        }

        /// <summary>列表合计（金额 / 税额 / 价税合计）。</summary>
        public async Task<InvoiceSummaryDto> GetSummaryAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            InvoiceStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            var invoices = await _invoiceRepository.GetRangeAsync(accountSetId, direction, startDate, endDate, status, cancellationToken);
            return new InvoiceSummaryDto
            {
                Count = invoices.Count,
                TotalAmount = invoices.Sum(i => i.TotalAmount),
                TotalTax = invoices.Sum(i => i.TotalTax),
                TotalWithTax = invoices.Sum(i => i.TotalWithTax),
            };
        }

        /// <summary>新建发票（发票号唯一，明细行整体提交）。</summary>
        public async Task<InvoiceDto> CreateAsync(Guid accountSetId, InvoiceDirection direction, SaveInvoiceRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
            if (await _invoiceRepository.ExistsByInvoiceNoAsync(accountSetId, direction, invoiceNo, cancellationToken: cancellationToken))
            {
                throw new InvoiceException($"发票号码「{invoiceNo}」已存在。");
            }

            Invoice invoice;
            try
            {
                invoice = Invoice.Create(
                    accountSetId,
                    direction,
                    request.InvoiceType,
                    request.InputDate,
                    request.IssueDate,
                    invoiceNo,
                    request.PartnerName,
                    request.BizType,
                    "手工新增",
                    request.Remark);
                invoice.ReplaceLines(request.Lines.Select(l => (l.GoodsName, l.Amount, l.TaxRate, l.TaxAmount)).ToList());
            }
            catch (ArgumentException ex)
            {
                throw new InvoiceException(ex.Message);
            }

            await _invoiceRepository.AddAsync(invoice, cancellationToken);
            await _invoiceRepository.SaveChangesAsync(cancellationToken);
            return ToDto(invoice);
        }

        /// <summary>修改发票；不存在时返回 null。</summary>
        public async Task<InvoiceDto?> UpdateAsync(Guid accountSetId, Guid id, SaveInvoiceRequest request, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (invoice is null)
            {
                return null;
            }

            var invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
            if (await _invoiceRepository.ExistsByInvoiceNoAsync(accountSetId, invoice.Direction, invoiceNo, invoice.Id, cancellationToken))
            {
                throw new InvoiceException($"发票号码「{invoiceNo}」已存在。");
            }

            try
            {
                invoice.Update(
                    request.InvoiceType,
                    request.InputDate,
                    request.IssueDate,
                    invoiceNo,
                    request.PartnerName,
                    request.BizType,
                    request.Remark);
                invoice.ReplaceLines(request.Lines.Select(l => (l.GoodsName, l.Amount, l.TaxRate, l.TaxAmount)).ToList());
            }
            catch (ArgumentException ex)
            {
                throw new InvoiceException(ex.Message);
            }

            await _invoiceRepository.SaveChangesAsync(cancellationToken);
            return ToDto(invoice);
        }

        /// <summary>删除发票；不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (invoice is null)
            {
                return false;
            }

            _invoiceRepository.Remove(invoice);
            await _invoiceRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>进项发票认证（登记认证日期与税款所属期）。</summary>
        public async Task<InvoiceDto?> AuthenticateAsync(Guid accountSetId, Guid id, AuthenticateInvoiceRequest request, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (invoice is null)
            {
                return null;
            }

            try
            {
                invoice.Authenticate(request.AuthDate, request.TaxPeriod);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvoiceException(ex.Message);
            }

            await _invoiceRepository.SaveChangesAsync(cancellationToken);
            return ToDto(invoice);
        }

        /// <summary>变更入账状态（入账 / 暂不入账）。</summary>
        public async Task<InvoiceDto?> ChangeStatusAsync(Guid accountSetId, Guid id, ChangeInvoiceStatusRequest request, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (invoice is null)
            {
                return null;
            }

            try
            {
                invoice.ChangeStatus((InvoiceStatus)request.Status);
            }
            catch (ArgumentException ex)
            {
                throw new InvoiceException(ex.Message);
            }

            await _invoiceRepository.SaveChangesAsync(cancellationToken);
            return ToDto(invoice);
        }

        /// <summary>
        /// 发票资金一览表：按往来单位归集期间内销项发票价税合计与资金日记账收款金额，
        /// 差额拆分为未开票金额 / 未收款金额。口径：收款 = 现金 + 银行存款日记账收入分录按往来单位归集。
        /// </summary>
        public async Task<IReadOnlyList<InvoiceFundOverviewRowDto>> GetFundOverviewAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var sales = await _invoiceRepository.GetRangeAsync(accountSetId, InvoiceDirection.Sales, startDate, endDate, cancellationToken: cancellationToken);
            var salesByPartner = sales
                .GroupBy(i => i.PartnerName)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.TotalWithTax));

            var entries = await _journalRepository.GetFundEntriesRangeAsync(accountSetId, startDate, endDate, cancellationToken);
            var receivedByPartner = entries
                .Where(x => x.Entry.Income > 0m && !string.IsNullOrWhiteSpace(x.Entry.Counterparty))
                .GroupBy(x => x.Entry.Counterparty!.Trim())
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Entry.Income));

            var partners = salesByPartner.Keys.Union(receivedByPartner.Keys);
            return partners
                .OrderBy(p => p)
                .Select(p =>
                {
                    salesByPartner.TryGetValue(p, out var salesAmount);
                    receivedByPartner.TryGetValue(p, out var received);
                    return new InvoiceFundOverviewRowDto
                    {
                        PartnerName = p,
                        SalesWithTax = salesAmount,
                        Received = received,
                    };
                })
                .ToList();
        }

        private static InvoiceDto ToDto(Invoice invoice) => new()
        {
            Id = invoice.Id,
            InvoiceType = invoice.InvoiceType,
            InputDate = invoice.InputDate,
            IssueDate = invoice.IssueDate,
            InvoiceNo = invoice.InvoiceNo,
            PartnerName = invoice.PartnerName,
            BizType = invoice.BizType,
            AuthDate = invoice.AuthDate,
            TaxPeriod = invoice.TaxPeriod,
            TotalAmount = invoice.TotalAmount,
            TotalTax = invoice.TotalTax,
            TotalWithTax = invoice.TotalWithTax,
            Source = invoice.Source,
            StatusText = InvoiceDto.StatusTextOf(invoice.Status),
            Lines = invoice.Lines
                .Select(l => new InvoiceLineDto
                {
                    Id = l.Id,
                    LineNumber = l.LineNumber,
                    GoodsName = l.GoodsName,
                    Amount = l.Amount,
                    TaxRate = l.TaxRate,
                    TaxAmount = l.TaxAmount,
                })
                .ToList(),
        };

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new InvoiceException("指定的账套不存在。");
            }
        }
    }
}
