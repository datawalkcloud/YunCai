using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>票据相关业务错误。</summary>
    public sealed class BillException : Exception
    {
        public BillException(string message) : base(message)
        {
        }
    }

    /// <summary>票据管理应用服务：按日期区间的台账查询 / 新建 / 修改 / 删除 / 流转状态。</summary>
    public sealed class BillService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IBillRepository _billRepository;

        public BillService(
            IAccountSetRepository accountSetRepository,
            IBillRepository billRepository)
        {
            _accountSetRepository = accountSetRepository;
            _billRepository = billRepository;
        }

        /// <summary>获取票据台账。</summary>
        public async Task<IReadOnlyList<BillDto>> GetRangeAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var bills = await _billRepository.GetRangeAsync(accountSetId, startDate, endDate, cancellationToken);
            return bills.Select(BillDto.FromDomain).ToList();
        }

        /// <summary>新建票据（票据号唯一）。</summary>
        public async Task<BillDto> CreateAsync(Guid accountSetId, SaveBillRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var billNo = request.BillNo?.Trim() ?? string.Empty;
            if (await _billRepository.ExistsByBillNoAsync(accountSetId, billNo, cancellationToken: cancellationToken))
            {
                throw new BillException($"票据号「{billNo}」已存在。");
            }

            Bill bill;
            try
            {
                bill = Bill.Create(
                    accountSetId,
                    request.Category,
                    request.IssueDate,
                    billNo,
                    request.Amount,
                    (BillKind)request.Kind,
                    request.DueDate,
                    request.Drawer,
                    request.Acceptor,
                    request.Transferable,
                    request.Remark);
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
            if (await _billRepository.ExistsByBillNoAsync(accountSetId, billNo, bill.Id, cancellationToken))
            {
                throw new BillException($"票据号「{billNo}」已存在。");
            }

            try
            {
                bill.Update(
                    request.Category,
                    request.IssueDate,
                    request.Amount,
                    (BillKind)request.Kind,
                    request.DueDate,
                    request.Drawer,
                    request.Acceptor,
                    request.Transferable,
                    request.Remark);
            }
            catch (ArgumentException ex)
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

        /// <summary>删除票据；不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var bill = await _billRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return false;
            }

            _billRepository.Remove(bill);
            await _billRepository.SaveChangesAsync(cancellationToken);
            return true;
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
