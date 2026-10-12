using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 自定义报表（数据透视表）应用服务：维护报表定义（编码/名称/取值口径），
    /// 并按定义实时渲染「科目 × 12 个月」的透视矩阵。
    /// 余额口径为各月末借-贷净额；发生额口径为各月借-贷净发生额；合计列余额口径为年末余额。
    /// </summary>
    public sealed class CustomReportService
    {
        private readonly ICustomReportRepository _repository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public CustomReportService(
            ICustomReportRepository repository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _repository = repository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>自定义报表定义列表（按编码排序）。</summary>
        public async Task<IReadOnlyList<CustomReportDto>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var reports = await _repository.GetAllAsync(accountSetId, cancellationToken);
            return reports.Select(ToDto).ToList();
        }

        /// <summary>新建报表定义（编码账套内唯一）。</summary>
        public async Task<CustomReportDto> CreateAsync(Guid accountSetId, SaveCustomReportRequest request, string operatorName, CancellationToken cancellationToken = default)
        {
            await EnsureCodeAvailableAsync(accountSetId, request.Code, null, cancellationToken);
            var report = CustomReport.Create(accountSetId, request.Code, request.Name, request.ValueType, operatorName);
            await _repository.AddAsync(report, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return ToDto(report);
        }

        /// <summary>更新报表定义（名称与取值口径；编码不可改）。</summary>
        public async Task<CustomReportDto?> UpdateAsync(Guid accountSetId, Guid id, SaveCustomReportRequest request, string operatorName, CancellationToken cancellationToken = default)
        {
            var report = await _repository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (report is null)
            {
                return null;
            }

            await EnsureCodeAvailableAsync(accountSetId, request.Code, id, cancellationToken);
            report.Update(request.Name, request.ValueType, operatorName);
            await _repository.SaveChangesAsync(cancellationToken);
            return ToDto(report);
        }

        /// <summary>删除报表定义。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var report = await _repository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (report is null)
            {
                return false;
            }

            _repository.Remove(report);
            await _repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>渲染报表：科目 × 12 个月的透视矩阵。year 省略时取最近有凭证的年份。</summary>
        public async Task<CustomReportRenderDto> RenderAsync(Guid accountSetId, Guid id, int? year, CancellationToken cancellationToken = default)
        {
            var report = await _repository.GetByIdAsync(accountSetId, id, cancellationToken)
                         ?? throw new ReportException("自定义报表不存在或已被删除。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var resolvedYear = year ?? (vouchers.Count > 0 ? vouchers.Max(v => v.Date).Year : DateTime.UtcNow.Year);
            if (resolvedYear < 1900 || resolvedYear > 9999)
            {
                throw new ReportException("年份不合法。");
            }

            // 逐科目逐月累计净发生额，再按口径合成为月值。
            var monthlyNet = new Dictionary<Guid, decimal[]>();
            foreach (var voucher in vouchers.Where(v => v.Date.Year == resolvedYear))
            {
                foreach (var entry in voucher.Entries)
                {
                    if (!monthlyNet.TryGetValue(entry.AccountId, out var months))
                    {
                        months = monthlyNet[entry.AccountId] = new decimal[12];
                    }

                    months[voucher.Date.Month - 1] += entry.Debit - entry.Credit;
                }
            }

            var rows = new List<CustomReportRenderRowDto>();
            foreach (var account in accounts.Where(a => a.IsLeaf).OrderBy(a => a.Code, StringComparer.Ordinal))
            {
                var months = monthlyNet.GetValueOrDefault(account.Id) ?? new decimal[12];
                var values = new decimal[12];
                decimal running = account.OpeningBalance;
                decimal total = 0m;
                for (var m = 0; m < 12; m++)
                {
                    running += months[m];
                    values[m] = Math.Round(
                        report.ValueType == CustomReportValueType.EndingBalance ? running : months[m],
                        2, MidpointRounding.ToEven);
                    total += months[m];
                }

                if (report.ValueType == CustomReportValueType.EndingBalance)
                {
                    total = running; // 余额口径的合计列为年末余额
                }

                // 全年无数据且期初为 0 的科目不展示。
                if (values.All(v => v == 0m) && total == 0m && account.OpeningBalance == 0m)
                {
                    continue;
                }

                rows.Add(new CustomReportRenderRowDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    Months = values,
                    Total = Math.Round(total, 2, MidpointRounding.ToEven),
                });
            }

            return new CustomReportRenderDto
            {
                Id = report.Id,
                Code = report.Code,
                Name = report.Name,
                ValueType = report.ValueType,
                Year = resolvedYear,
                Rows = rows,
            };
        }

        private async Task EnsureCodeAvailableAsync(Guid accountSetId, string code, Guid? excludeId, CancellationToken cancellationToken)
        {
            if (await _repository.CodeExistsAsync(accountSetId, code.Trim(), excludeId, cancellationToken))
            {
                throw new ReportException($"报表编码「{code.Trim()}」已存在。");
            }
        }

        private static CustomReportDto ToDto(CustomReport report)
        {
            return new CustomReportDto
            {
                Id = report.Id,
                Code = report.Code,
                Name = report.Name,
                ValueType = report.ValueType,
                CreatedBy = report.CreatedBy,
                CreatedAt = report.CreatedAt,
                ModifiedBy = report.ModifiedBy,
                ModifiedAt = report.ModifiedAt,
            };
        }
    }
}
