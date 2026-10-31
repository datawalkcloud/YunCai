using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Invoices.Domain;
using KuaiJi.Modules.Invoices.Infrastructure;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Vouchers.Domain;

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
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public InvoiceService(
            IAccountSetRepository accountSetRepository,
            IInvoiceRepository invoiceRepository,
            IJournalRepository journalRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _invoiceRepository = invoiceRepository;
            _journalRepository = journalRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
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

            // 关联凭证字号需要凭证实体；顺带自愈指向已删除凭证的残留引用。
            var vouchersById = await LoadVouchersByIdAsync(accountSetId, cancellationToken);
            await HealStaleVoucherLinksAsync(invoices, vouchersById, cancellationToken);
            return invoices.Select(i => ToDto(i, VoucherTextOf(i, vouchersById))).ToList();
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
            return ToDto(invoice, await VoucherTextAsync(invoice, cancellationToken));
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
            return ToDto(invoice, await VoucherTextAsync(invoice, cancellationToken));
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

        /// <summary>批量删除发票；不存在的计入跳过。</summary>
        public async Task<BulkInvoiceResultDto> DeleteManyAsync(
            Guid accountSetId,
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            var affected = 0;
            var skipped = 0;
            foreach (var id in ids.Distinct())
            {
                if (await DeleteAsync(accountSetId, id, cancellationToken))
                {
                    affected++;
                }
                else
                {
                    skipped++;
                }
            }

            return new BulkInvoiceResultDto { Affected = affected, Skipped = skipped };
        }

        /// <summary>批量变更入账状态；不存在的计入跳过。状态非法时整批拒绝。</summary>
        public async Task<BulkInvoiceResultDto> ChangeStatusManyAsync(
            Guid accountSetId,
            IReadOnlyList<Guid> ids,
            InvoiceStatus status,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(status))
            {
                throw new InvoiceException("发票状态无效。");
            }

            var affected = 0;
            var skipped = 0;
            foreach (var id in ids.Distinct())
            {
                var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (invoice is null)
                {
                    skipped++;
                    continue;
                }

                invoice.ChangeStatus(status);
                affected++;
            }

            if (affected > 0)
            {
                await _invoiceRepository.SaveChangesAsync(cancellationToken);
            }

            return new BulkInvoiceResultDto { Affected = affected, Skipped = skipped };
        }

        /// <summary>批量认证进项发票；销项发票与不存在的计入跳过（不整批失败）。</summary>
        public async Task<BulkInvoiceResultDto> AuthenticateManyAsync(
            Guid accountSetId,
            IReadOnlyList<Guid> ids,
            DateOnly authDate,
            int? taxPeriod,
            CancellationToken cancellationToken = default)
        {
            var affected = 0;
            var skipped = 0;
            foreach (var id in ids.Distinct())
            {
                var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);

                // 仅进项发票可认证；销项发票跳过而不是抛错，避免整批失败。
                if (invoice is null || invoice.Direction != InvoiceDirection.Purchase)
                {
                    skipped++;
                    continue;
                }

                invoice.Authenticate(authDate, taxPeriod);
                affected++;
            }

            if (affected > 0)
            {
                await _invoiceRepository.SaveChangesAsync(cancellationToken);
            }

            return new BulkInvoiceResultDto { Affected = affected, Skipped = skipped };
        }

        /// <summary>
        /// 导入发票：按「发票号码」把多个商品明细行聚合成一张发票，
        /// 发票号在同一方向内重复时整张跳过并告警；金额非正数、缺往来单位的行同样跳过。
        /// 明细行税率或税额缺失时按税率反算税额。
        /// </summary>
        public async Task<InvoiceImportResult> ImportAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            IReadOnlyList<InvoiceImportRow> rows,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            if (rows.Count == 0)
            {
                return new InvoiceImportResult();
            }

            var warnings = new List<string>();
            var skipped = 0;
            var imported = 0;

            // 同一方向内已有的发票号，避免与库中数据冲突。
            var existing = new HashSet<string>(
                (await _invoiceRepository.GetRangeAsync(accountSetId, direction, null, null, null, cancellationToken))
                    .Select(i => i.InvoiceNo),
                StringComparer.OrdinalIgnoreCase);

            // 按发票号聚合（保持首次出现的顺序）。
            var grouped = new List<(string InvoiceNo, List<InvoiceImportRow> Lines)>();
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var no = row.InvoiceNo?.Trim() ?? string.Empty;
                if (no.Length == 0)
                {
                    skipped++;
                    warnings.Add("存在发票号码为空的行，已跳过。");
                    continue;
                }

                if (!order.TryGetValue(no, out var idx))
                {
                    idx = grouped.Count;
                    order[no] = idx;
                    grouped.Add((no, []));
                }

                grouped[idx].Lines.Add(row);
            }

            foreach (var (invoiceNo, lines) in grouped)
            {
                // 票头取该发票首行的值；缺开票日期时退回录入日期。
                var head = lines[0];
                var issueDate = head.IssueDate ?? head.InputDate;
                var inputDate = head.InputDate ?? issueDate;
                var partner = head.PartnerName?.Trim();

                if (issueDate is null)
                {
                    skipped += lines.Count;
                    warnings.Add($"发票「{invoiceNo}」缺少开票日期，已跳过。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(partner))
                {
                    skipped += lines.Count;
                    warnings.Add($"发票「{invoiceNo}」缺少{directionText(direction)}名称，已跳过。");
                    continue;
                }

                if (existing.Contains(invoiceNo))
                {
                    skipped += lines.Count;
                    warnings.Add($"发票号码「{invoiceNo}」已存在，已跳过。");
                    continue;
                }

                var parsed = new List<(string goodsName, decimal amount, decimal taxRate, decimal taxAmount)>();
                var invalid = false;
                foreach (var line in lines)
                {
                    if (line.Amount <= 0m)
                    {
                        invalid = true;
                        warnings.Add($"发票「{invoiceNo}」存在金额不是正数的明细行，已跳过整张发票。");
                        break;
                    }

                    var taxAmount = line.TaxAmount ?? Math.Round(line.Amount * line.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
                    parsed.Add((line.GoodsName?.Trim() ?? "商品", line.Amount, line.TaxRate, taxAmount));
                }

                if (invalid)
                {
                    skipped += lines.Count;
                    continue;
                }

                try
                {
                    var invoice = Invoice.Create(
                        accountSetId,
                        direction,
                        string.IsNullOrWhiteSpace(head.InvoiceType) ? "增值税专用发票" : head.InvoiceType.Trim(),
                        inputDate.Value,
                        issueDate.Value,
                        invoiceNo,
                        partner,
                        head.BizType,
                        "导入",
                        null);
                    invoice.ReplaceLines(parsed);

                    if (direction == InvoiceDirection.Purchase && head.AuthDate is { } authDate)
                    {
                        invoice.Authenticate(authDate, head.TaxPeriod);
                    }

                    await _invoiceRepository.AddAsync(invoice, cancellationToken);
                    existing.Add(invoiceNo);
                    imported++;
                }
                catch (ArgumentException ex)
                {
                    skipped += lines.Count;
                    warnings.Add($"发票「{invoiceNo}」导入失败：{ex.Message}");
                }
            }

            if (imported > 0)
            {
                await _invoiceRepository.SaveChangesAsync(cancellationToken);
            }

            // 告警去重，避免同一条提示重复几十行。
            return new InvoiceImportResult
            {
                TotalRows = rows.Count,
                Imported = imported,
                Skipped = skipped,
                Warnings = warnings.Distinct().Take(20).ToList(),
            };

            static string directionText(InvoiceDirection direction)
                => direction == InvoiceDirection.Sales ? "客户" : "供应商";
        }

        /// <summary>
        /// 由发票生成一张凭证并回写关联：
        /// 销项 借 对方科目 价税合计 / 贷 收入科目 金额、贷 销项税额科目 税额；
        /// 进项 借 商品科目 金额、借 进项税额科目 税额 / 贷 对方科目 价税合计。
        /// 已入账（已生成凭证）或暂不入账的发票不再重复出票；税额为 0 时不产生税额行。
        /// </summary>
        public async Task<InvoiceVoucherResultDto> GenerateVoucherAsync(
            Guid accountSetId,
            Guid id,
            string preparer,
            GenerateInvoiceVoucherRequest request,
            CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                          ?? throw new InvoiceException("指定的发票不存在。");

            if (invoice.Status == InvoiceStatus.Posted)
            {
                throw new InvoiceException("该发票已入账，请勿重复生成凭证。");
            }

            if (invoice.Status == InvoiceStatus.Suspended)
            {
                throw new InvoiceException("该发票已标记暂不入账，如需入账请先恢复为未入账。");
            }

            var amount = invoice.TotalAmount;
            var tax = invoice.TotalTax;
            var withTax = invoice.TotalWithTax;
            if (amount == 0m && tax == 0m)
            {
                throw new InvoiceException("发票金额为 0，无法生成凭证。");
            }

            var isSales = invoice.Direction == InvoiceDirection.Sales;
            var counter = await ResolveAccountAsync(accountSetId, request.CounterAccountId,
                isSales ? "1122" : "2202", isSales ? "应收账款" : "应付账款", cancellationToken);
            var subject = await ResolveAccountAsync(accountSetId, request.SubjectAccountId,
                isSales ? "5001" : "1405", isSales ? "主营业务收入" : "库存商品", cancellationToken);

            Account? taxAccount = null;
            if (tax != 0m)
            {
                taxAccount = await ResolveAccountAsync(accountSetId, request.TaxAccountId,
                    isSales ? "222100105" : "222100101", isSales ? "销项税额" : "进项税额", cancellationToken);
            }

            var word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim();
            var date = request.Date ?? invoice.IssueDate;
            var number = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;

            var voucher = Voucher.Create(accountSetId, word, number, date, preparer);
            var summary = $"{invoice.PartnerName}{(string.IsNullOrWhiteSpace(invoice.BizType) ? "" : $"-{invoice.BizType}")}";

            if (isSales)
            {
                voucher.AddEntry(summary, counter.Id, withTax, 0m);
                voucher.AddEntry(summary, subject.Id, 0m, amount);
                if (taxAccount is not null && tax != 0m) voucher.AddEntry(summary, taxAccount.Id, 0m, tax);
            }
            else
            {
                voucher.AddEntry(summary, subject.Id, amount, 0m);
                if (taxAccount is not null && tax != 0m) voucher.AddEntry(summary, taxAccount.Id, tax, 0m);
                voucher.AddEntry(summary, counter.Id, 0m, withTax);
            }

            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            invoice.AttachVoucher(voucher.Id);
            invoice.ChangeStatus(InvoiceStatus.Posted);
            await _invoiceRepository.SaveChangesAsync(cancellationToken);

            return new InvoiceVoucherResultDto
            {
                InvoiceId = invoice.Id,
                VoucherId = voucher.Id,
                VoucherNo = $"{voucher.Word}-{voucher.Number}",
            };
        }

        /// <summary>
        /// 批量由发票生成凭证：逐条出票，单条失败不影响其余。
        /// 已入账 / 暂不入账的发票计入跳过而非失败（幂等，重复勾选不报错）。
        /// </summary>
        public async Task<InvoiceVoucherBatchResultDto> GenerateVouchersAsync(
            Guid accountSetId,
            IReadOnlyList<Guid> ids,
            string preparer,
            GenerateInvoiceVoucherRequest request,
            CancellationToken cancellationToken = default)
        {
            var created = 0;
            var skipped = 0;
            var failures = new List<InvoiceVoucherFailureDto>();

            foreach (var id in ids.Distinct())
            {
                var invoice = await _invoiceRepository.GetByIdAsync(accountSetId, id, cancellationToken);

                // 不存在、已入账、暂不入账：不重复出票，计入跳过。
                if (invoice is null || invoice.Status != InvoiceStatus.Pending)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    await GenerateVoucherAsync(accountSetId, id, preparer, request, cancellationToken);
                    created++;
                }
                catch (InvoiceException ex)
                {
                    failures.Add(new InvoiceVoucherFailureDto
                    {
                        InvoiceId = id,
                        InvoiceNo = invoice.InvoiceNo,
                        Reason = ex.Message,
                    });
                }
            }

            return new InvoiceVoucherBatchResultDto
            {
                Created = created,
                Skipped = skipped,
                Failures = failures,
            };
        }

        /// <summary>
        /// 解析科目：显式指定时按 Id 取（须为末级且未停用）；
        /// 未指定时按科目编码取默认科目，取不到则给出可操作的提示。
        /// </summary>
        private async Task<Account> ResolveAccountAsync(
            Guid accountSetId,
            Guid? explicitId,
            string defaultCode,
            string roleName,
            CancellationToken cancellationToken)
        {
            var account = explicitId is { } id
                ? await _accountRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                : await _accountRepository.GetByCodeAsync(accountSetId, defaultCode, cancellationToken);

            if (account is null)
            {
                throw new InvoiceException(
                    explicitId is null
                        ? $"未找到{roleName}科目（编码 {defaultCode}），请在生成凭证时手动指定科目。"
                        : $"指定的{roleName}科目不存在。");
            }

            if (!account.IsLeaf)
            {
                // 默认科目在部分账套里被拆成了往来单位明细（如 2202 下挂 2202003），
                // 此时不能替用户猜测，明确引导其在生成凭证时手动指定下级末级科目。
                throw new InvoiceException(
                    $"科目「{account.Code} {account.Name}」不是末级科目，不能用于记账，请在生成凭证时手动指定其下级末级科目。");
            }

            if (!account.IsEnabled)
            {
                throw new InvoiceException($"科目「{account.Code} {account.Name}」已停用，不能用于记账。");
            }

            return account;
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
            return ToDto(invoice, await VoucherTextAsync(invoice, cancellationToken));
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
            return ToDto(invoice, await VoucherTextAsync(invoice, cancellationToken));
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

        /// <summary>导出发票（.xlsx）；按商品明细行展开，方向与列表一致。</summary>
        public async Task<byte[]> ExportAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            InvoiceStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            var invoices = await GetRangeAsync(accountSetId, direction, startDate, endDate, status, cancellationToken);
            return InvoiceExportBuilder.Build(invoices, direction);
        }

        /// <summary>导出「发票资金一览表」（.xlsx）。</summary>
        public async Task<byte[]> ExportFundOverviewAsync(
            Guid accountSetId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var rows = await GetFundOverviewAsync(accountSetId, startDate, endDate, cancellationToken);
            return InvoiceExportBuilder.BuildFundOverview(rows);
        }

        /// <summary>
        /// 加载仍在册的凭证（按 Id 索引）。只认 DeletedAt 为空的凭证：
        /// 回收站中的凭证不算有效关联，否则「关联凭证」列会显示空白。
        /// </summary>
        private async Task<IReadOnlyDictionary<Guid, Voucher>> LoadVouchersByIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            return vouchers.Where(v => v.DeletedAt is null).ToDictionary(v => v.Id, v => v);
        }

        /// <summary>
        /// 自愈：清除发票中指向已删除凭证的残留引用，有变更时落库（无变更为零开销）。
        /// 否则发票会因「已入账」被永久排除，既看不到关联凭证也无法重新生成。
        /// </summary>
        private async Task HealStaleVoucherLinksAsync(
            IReadOnlyList<Invoice> invoices,
            IReadOnlyDictionary<Guid, Voucher> vouchersById,
            CancellationToken cancellationToken)
        {
            // 列表查询是 AsNoTracking，须显式标记修改，否则自愈结果不会落库。
            var stale = invoices.Where(i => i.ClearStaleVoucher(vouchersById.Keys.ToHashSet())).ToList();
            if (stale.Count > 0)
            {
                foreach (var invoice in stale)
                {
                    _invoiceRepository.Update(invoice);
                }

                await _invoiceRepository.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>关联凭证字号文本（形如「记-12」）；未关联或凭证已删除时为 null。</summary>
        private static string? VoucherTextOf(Invoice invoice, IReadOnlyDictionary<Guid, Voucher> vouchersById)
            => invoice.VoucherId is { } id && vouchersById.TryGetValue(id, out var voucher)
                ? $"{voucher.Word}-{voucher.Number}"
                : null;

        /// <summary>
        /// 单条发票的关联凭证字号：只查这一张凭证，供新建 / 修改 / 认证 / 状态变更等
        /// 单条写操作回填，避免逐条循环时反复加载全部凭证。
        /// </summary>
        private async Task<string?> VoucherTextAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            if (invoice.VoucherId is not { } id)
            {
                return null;
            }

            var voucher = await _voucherRepository.GetByIdAsync(invoice.AccountSetId, id, cancellationToken);
            return voucher is null || voucher.DeletedAt is not null ? null : $"{voucher.Word}-{voucher.Number}";
        }

        private static InvoiceDto ToDto(Invoice invoice, string? voucherText = null) => new()
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
            VoucherText = voucherText,
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
