using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>票据相关业务错误。</summary>
    public sealed class BillException : Exception
    {
        public BillException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 票据管理应用服务：按日期区间的台账查询 / 新建 / 修改 / 删除 / 流转状态，
    /// 以及票据生成凭证（收款凭证 / 贴现凭证）、票面附件与导入导出。
    /// </summary>
    public sealed class BillService
    {
        /// <summary>应收票据默认科目编码。</summary>
        public const string ReceivableBillCode = "1121";

        /// <summary>银行存款默认科目编码。</summary>
        public const string BankDepositCode = "1002";

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IBillRepository _billRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public BillService(
            IAccountSetRepository accountSetRepository,
            IBillRepository billRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _billRepository = billRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>获取票据台账。</summary>
        public async Task<IReadOnlyList<BillDto>> GetRangeAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var bills = await _billRepository.GetRangeAsync(accountSetId, startDate, endDate, cancellationToken);
            var attachmentCounts = await _billRepository.CountAttachmentsAsync(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            var voucherLabels = vouchers
                .Where(v => !v.IsDeleted)
                .ToDictionary(v => v.Id, v => $"{v.Word}-{v.Number}");

            return bills
                .Select(b => BillDto.FromDomain(
                    b,
                    attachmentCounts.GetValueOrDefault(b.Id),
                    b.VoucherId is { } vid ? voucherLabels.GetValueOrDefault(vid) : null))
                .ToList();
        }

        /// <summary>新建票据（票据号唯一）。</summary>
        public async Task<BillDto> CreateAsync(Guid accountSetId, SaveBillRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var billNo = request.BillNo?.Trim() ?? string.Empty;
            if (await _billRepository.ExistsByBillNoAsync(accountSetId, billNo, cancellationToken: cancellationToken))
            {
                throw new BillException($"票据号码「{billNo}」已存在。");
            }

            Bill bill;
            try
            {
                bill = Bill.Create(
                    accountSetId,
                    request.Category ?? string.Empty,
                    request.IssueDate,
                    billNo,
                    request.Amount,
                    (BillKind)request.Kind,
                    request.DueDate,
                    request.Drawer,
                    request.Acceptor,
                    request.Transferable,
                    request.Remark);
                bill.FillParties(
                    request.Payee,
                    request.DrawerAccountNo,
                    request.PayeeAccountNo,
                    request.AcceptorAccountNo,
                    request.DrawerBank,
                    request.PayeeBank,
                    request.AcceptorBank);
                if (request.Status is { } status && status != (int)BillStatus.Holding)
                {
                    bill.ChangeStatus((BillStatus)status);
                }
            }
            catch (ArgumentException ex)
            {
                throw new BillException(ex.Message);
            }

            await _billRepository.AddAsync(bill, cancellationToken);
            await _billRepository.SaveChangesAsync(cancellationToken);
            return BillDto.FromDomain(bill);
        }

        /// <summary>修改票据；不存在时返回 null。</summary>
        public async Task<BillDto?> UpdateAsync(Guid accountSetId, Guid id, SaveBillRequest request, CancellationToken cancellationToken = default)
        {
            var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return null;
            }

            var billNo = request.BillNo?.Trim() ?? string.Empty;
            if (!string.Equals(billNo, bill.BillNo, StringComparison.Ordinal)
                && await _billRepository.ExistsByBillNoAsync(accountSetId, billNo, bill.Id, cancellationToken))
            {
                throw new BillException($"票据号码「{billNo}」已存在。");
            }

            try
            {
                bill.ChangeBillNo(billNo);
                bill.Update(
                    request.Category ?? string.Empty,
                    request.IssueDate,
                    request.Amount,
                    (BillKind)request.Kind,
                    request.DueDate,
                    request.Drawer,
                    request.Acceptor,
                    request.Transferable,
                    request.Remark);
                bill.FillParties(
                    request.Payee,
                    request.DrawerAccountNo,
                    request.PayeeAccountNo,
                    request.AcceptorAccountNo,
                    request.DrawerBank,
                    request.PayeeBank,
                    request.AcceptorBank);
                if (request.Status is { } status && status != (int)bill.Status)
                {
                    bill.ChangeStatus((BillStatus)status);
                }
            }
            catch (ArgumentException ex)
            {
                throw new BillException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new BillException(ex.Message);
            }

            await _billRepository.SaveChangesAsync(cancellationToken);
            return BillDto.FromDomain(bill);
        }

        /// <summary>流转票据状态；不存在时返回 null。</summary>
        public async Task<BillDto?> ChangeStatusAsync(Guid accountSetId, Guid id, ChangeBillStatusRequest request, CancellationToken cancellationToken = default)
        {
            var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return null;
            }

            try
            {
                bill.ChangeStatus((BillStatus)request.Status);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                throw new BillException(ex.Message);
            }

            await _billRepository.SaveChangesAsync(cancellationToken);
            return BillDto.FromDomain(bill);
        }

        /// <summary>收款：把票据置为「票据已结清」。已结清时提示，与柠檬云一致。</summary>
        public async Task<BillDto?> CollectAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return null;
            }

            if (bill.Status == BillStatus.Cashed)
            {
                throw new BillException("亲，票据已收款，无需重复收款。");
            }

            if (bill.Status == BillStatus.Voided)
            {
                throw new BillException("票据已作废，不能收款。");
            }

            try
            {
                bill.ChangeStatus(BillStatus.Cashed);
            }
            catch (InvalidOperationException ex)
            {
                throw new BillException(ex.Message);
            }

            await _billRepository.SaveChangesAsync(cancellationToken);
            return BillDto.FromDomain(bill);
        }

        /// <summary>转让（背书）：已结清或不可转让的票据不能转让。</summary>
        public async Task<BillDto?> EndorseAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return null;
            }

            if (bill.Status == BillStatus.Cashed)
            {
                throw new BillException("亲，票据已收款，无法转让。");
            }

            if (!bill.Transferable)
            {
                throw new BillException("该票据不可转让。");
            }

            if (bill.Status == BillStatus.Voided)
            {
                throw new BillException("票据已作废，不能转让。");
            }

            try
            {
                bill.ChangeStatus(BillStatus.Endorsing);
            }
            catch (InvalidOperationException ex)
            {
                throw new BillException(ex.Message);
            }

            await _billRepository.SaveChangesAsync(cancellationToken);
            return BillDto.FromDomain(bill);
        }

        /// <summary>删除票据；不存在时返回 false。已生成凭证的票据不允许删除（避免残留无主凭证）。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return false;
            }

            if (bill.VoucherId is { } voucherId)
            {
                var voucher = await _voucherRepository.GetByIdAsync(accountSetId, voucherId, cancellationToken);
                if (voucher is { IsDeleted: false })
                {
                    throw new BillException("该票据已生成凭证，不能删除；如需删除请先删除对应凭证。");
                }
            }

            _billRepository.Remove(bill);
            await _billRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>批量删除票据，返回实际删除条数；已生成凭证的跳过。</summary>
        public async Task<int> DeleteManyAsync(Guid accountSetId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        {
            if (ids.Count == 0)
            {
                throw new BillException("请先勾选要操作的票据。");
            }

            var removed = 0;
            var blocked = 0;
            foreach (var id in ids.Distinct())
            {
                try
                {
                    if (await DeleteAsync(accountSetId, id, cancellationToken))
                    {
                        removed++;
                    }
                }
                catch (BillException)
                {
                    blocked++;
                }
            }

            if (removed == 0 && blocked > 0)
            {
                throw new BillException("勾选的票据均已生成凭证，不能删除；如需删除请先删除对应凭证。");
            }

            return removed;
        }

        /// <summary>
        /// 批量生成凭证：已生成凭证、已作废、未设置收支类别的票据跳过（与柠檬云提示一致），
        /// 单条失败不阻断整批。返回成功与跳过数量。
        /// </summary>
        public async Task<BulkBillVoucherResult> GenerateVouchersAsync(
            Guid accountSetId,
            IReadOnlyCollection<Guid> ids,
            string voucherKind,
            string preparer,
            string? word,
            CancellationToken cancellationToken = default)
        {
            if (ids.Count == 0)
            {
                throw new BillException("请选择票据后生成凭证。");
            }

            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var moneyAccount = PickLeaf(accounts, BankDepositCode)
                               ?? PickLeaf(accounts, "1001")
                               ?? throw new BillException("未找到银行存款科目（1002），请先在会计科目中维护。");
            var billAccount = PickLeaf(accounts, ReceivableBillCode)
                              ?? throw new BillException($"未找到应收票据科目（{ReceivableBillCode}），请先在会计科目中维护。");

            var finalWord = string.IsNullOrWhiteSpace(word) ? "记" : word.Trim();
            var isDiscount = string.Equals(voucherKind, "discount", StringComparison.OrdinalIgnoreCase);
            var summaryPrefix = isDiscount ? "票据贴现" : "票据收款";

            var created = 0;
            var skipped = 0;
            foreach (var id in ids.Distinct())
            {
                var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (bill is null)
                {
                    skipped++;
                    continue;
                }

                if (bill.HasVoucher || bill.Status == BillStatus.Voided || string.IsNullOrWhiteSpace(bill.Category))
                {
                    skipped++;
                    continue;
                }

                var summary = $"{summaryPrefix}-{bill.BillNo}";
                var number = await _voucherRepository.GetMaxNumberAsync(accountSetId, finalWord, cancellationToken) + 1;

                Voucher voucher;
                try
                {
                    voucher = Voucher.Create(accountSetId, finalWord, number, bill.IssueDate, preparer);
                    // 借 银行存款 / 贷 应收票据。
                    voucher.AddEntry(summary, moneyAccount.Id, bill.Amount, 0m);
                    voucher.AddEntry(summary, billAccount.Id, 0m, bill.Amount);
                }
                catch (ArgumentException ex)
                {
                    throw new BillException(ex.Message);
                }

                await _voucherRepository.AddAsync(voucher, cancellationToken);
                await _voucherRepository.SaveChangesAsync(cancellationToken);

                bill.AttachVoucher(voucher.Id);
                await _billRepository.SaveChangesAsync(cancellationToken);
                created++;
            }

            return new BulkBillVoucherResult { Created = created, Skipped = skipped };
        }

        /// <summary>列出票据附件。</summary>
        public async Task<IReadOnlyList<BillAttachmentInfo>> ListAttachmentsAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            await EnsureBillAsync(accountSetId, id, cancellationToken);
            return await _billRepository.ListAttachmentsAsync(id, cancellationToken);
        }

        /// <summary>上传票据附件，返回附件摘要。</summary>
        public async Task<BillAttachmentInfo> AddAttachmentAsync(Guid accountSetId, Guid id, string fileName, string? contentType, byte[] content, CancellationToken cancellationToken = default)
        {
            await EnsureBillAsync(accountSetId, id, cancellationToken);

            BillAttachment attachment;
            try
            {
                attachment = BillAttachment.Create(id, fileName, contentType, content);
            }
            catch (ArgumentException ex)
            {
                throw new BillException(ex.Message);
            }

            await _billRepository.AddAttachmentAsync(attachment, cancellationToken);
            await _billRepository.SaveChangesAsync(cancellationToken);
            return new BillAttachmentInfo(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedAt);
        }

        /// <summary>下载票据附件；不存在返回 null。</summary>
        public async Task<BillAttachment?> GetAttachmentAsync(Guid accountSetId, Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            await EnsureBillAsync(accountSetId, id, cancellationToken);
            return await _billRepository.GetAttachmentAsync(id, attachmentId, cancellationToken);
        }

        /// <summary>删除票据附件；不存在返回 false。</summary>
        public async Task<bool> DeleteAttachmentAsync(Guid accountSetId, Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            await EnsureBillAsync(accountSetId, id, cancellationToken);
            var attachment = await _billRepository.GetAttachmentAsync(id, attachmentId, cancellationToken);
            if (attachment is null)
            {
                return false;
            }

            _billRepository.RemoveAttachment(attachment);
            await _billRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// 按科目编码取可用于记账的末级科目：编码本身即末级且启用时直接用，
        /// 否则取它的第一个启用的末级下级（如 1002 银行存款 → 1002001 工行）。
        /// </summary>
        private static Account? PickLeaf(IReadOnlyList<Account> accounts, string code)
        {
            var account = accounts.FirstOrDefault(a => a.Code == code);
            if (account is null)
            {
                return null;
            }

            if (account.IsLeaf && account.IsEnabled)
            {
                return account;
            }

            return accounts
                .Where(a => a.ParentId == account.Id && a.IsLeaf && a.IsEnabled)
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private async Task<Bill> EnsureBillAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken)        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            return await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                   ?? throw new BillException("指定的票据不存在。");
        }

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new BillException("指定的账套不存在。");
            }
        }
    }
}
