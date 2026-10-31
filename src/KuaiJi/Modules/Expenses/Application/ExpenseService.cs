using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Expenses.Domain;
using KuaiJi.Modules.Expenses.Infrastructure;
using KuaiJi.Modules.Vouchers.Application;
using System.Globalization;

namespace KuaiJi.Modules.Expenses.Application
{
    /// <summary>
    /// 费用单据应用服务：登记 / 修改 / 删除 / 导入导出 / 生成凭证。
    /// 凭证口径：借 费用科目（不含税额）+ 借 进项税额（可抵扣税额）/ 贷 对方科目（含税总额）。
    /// </summary>
    public sealed class ExpenseService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IExpenseRepository _expenseRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly VoucherService _voucherService;

        public ExpenseService(
            IAccountSetRepository accountSetRepository,
            IExpenseRepository expenseRepository,
            IAccountRepository accountRepository,
            VoucherService voucherService)
        {
            _accountSetRepository = accountSetRepository;
            _expenseRepository = expenseRepository;
            _accountRepository = accountRepository;
            _voucherService = voucherService;
        }

        /// <summary>获取全部费用单据（附关联凭证字号，凭证删除后自愈）。</summary>
        public async Task<IReadOnlyList<ExpenseBillDto>> GetBillsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var bills = await _expenseRepository.GetBillsAsync(accountSetId, cancellationToken);
            var vouchersById = (await _expenseRepository.GetVoucherRefsAsync(accountSetId, cancellationToken))
                .ToDictionary(v => v.Id, v => v);

            var stale = bills.Where(b => b.ClearStaleVoucher(vouchersById.Keys.ToHashSet())).ToList();
            var drifted = bills.Where(b => b.SyncStatus()).ToList();
            if (stale.Count > 0 || drifted.Count > 0)
            {
                await _expenseRepository.SaveChangesAsync(cancellationToken);
            }

            return bills.Select(b => ToDto(b, vouchersById)).ToList();
        }

        /// <summary>列表合计。</summary>
        public async Task<ExpenseBillSummaryDto> GetSummaryAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var bills = await GetBillsAsync(accountSetId, cancellationToken);
            return new ExpenseBillSummaryDto
            {
                Count = bills.Count,
                TotalAmount = bills.Sum(b => b.Amount),
                TotalDeductibleTax = bills.Sum(b => b.DeductibleTax),
            };
        }

        /// <summary>新建费用单据（编号留空自动生成，形如 FY-0001）。</summary>
        public async Task<ExpenseBillDto> CreateBillAsync(Guid accountSetId, SaveExpenseBillRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var billNo = request.BillNo?.Trim() ?? string.Empty;
            if (billNo.Length > 0 && await _expenseRepository.BillNoExistsAsync(accountSetId, billNo, cancellationToken: cancellationToken))
            {
                throw new ExpenseException($"单据编号「{billNo}」已存在。");
            }

            var bill = BuildBill(accountSetId, request);
            if (billNo.Length == 0)
            {
                bill.AssignBillNo(await GenerateBillNoAsync(accountSetId, cancellationToken));
            }

            await _expenseRepository.AddBillAsync(bill, cancellationToken);
            await _expenseRepository.SaveChangesAsync(cancellationToken);
            return ToDto(bill, new Dictionary<Guid, VoucherRef>());
        }

        /// <summary>修改费用单据；已入账单据不可修改。不存在时返回 null。</summary>
        public async Task<ExpenseBillDto?> UpdateBillAsync(Guid accountSetId, Guid id, SaveExpenseBillRequest request, CancellationToken cancellationToken = default)
        {
            var bill = await _expenseRepository.GetBillForUpdateAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return null;
            }

            var billNo = request.BillNo?.Trim() ?? string.Empty;
            if (billNo.Length > 0 && billNo != bill.BillNo && await _expenseRepository.BillNoExistsAsync(accountSetId, billNo, bill.Id, cancellationToken))
            {
                throw new ExpenseException($"单据编号「{billNo}」已存在。");
            }

            try
            {
                bill.Update(request.BillDate, request.Reason, request.ExpenseType, request.Payee, request.SettleMethod, request.Amount, request.DeductibleTax);
            }
            catch (InvalidOperationException ex)
            {
                throw new ExpenseException(Plain(ex));
            }
            catch (ArgumentException ex)
            {
                throw new ExpenseException(Plain(ex));
            }

            await _expenseRepository.SaveChangesAsync(cancellationToken);
            return ToDto(bill, new Dictionary<Guid, VoucherRef>());
        }

        /// <summary>删除费用单据；已入账单据不可删除。不存在时返回 false。</summary>
        public async Task<bool> DeleteBillAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var bill = await _expenseRepository.GetBillForUpdateAsync(accountSetId, id, cancellationToken);
            if (bill is null)
            {
                return false;
            }

            if (bill.Status == ExpenseBillStatus.Posted)
            {
                throw new ExpenseException($"单据「{bill.BillNo}」已生成凭证，请先删除凭证再删除单据。");
            }

            _expenseRepository.RemoveBill(bill);
            await _expenseRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>批量删除费用单据；已入账的计入跳过。</summary>
        public async Task<(int Affected, int Skipped, IReadOnlyList<string> Messages)> DeleteBillsAsync(Guid accountSetId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        {
            var affected = 0;
            var skipped = 0;
            var messages = new List<string>();
            foreach (var id in ids.Distinct())
            {
                try
                {
                    if (await DeleteBillAsync(accountSetId, id, cancellationToken))
                    {
                        affected++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (ExpenseException ex)
                {
                    skipped++;
                    messages.Add(ex.Message);
                }
            }

            return (affected, skipped, messages);
        }

        // —— 生成凭证 ——

        /// <summary>为一张费用单据生成凭证并回填关联。</summary>
        public async Task<string> GenerateVoucherAsync(Guid accountSetId, Guid billId, GenerateExpenseVoucherRequest request, CancellationToken cancellationToken = default)
        {
            var bill = await _expenseRepository.GetBillForUpdateAsync(accountSetId, billId, cancellationToken)
                       ?? throw new ExpenseException("费用单据不存在。");
            var text = await GenerateForBillAsync(accountSetId, bill, request, cancellationToken);
            await _expenseRepository.SaveChangesAsync(cancellationToken);
            return text;
        }

        /// <summary>为多张费用单据批量生成凭证；单张失败计入失败明细，不整批失败。</summary>
        public async Task<ExpenseVoucherBatchResultDto> GenerateVouchersAsync(Guid accountSetId, GenerateExpenseVouchersRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var bills = await _expenseRepository.GetBillsForUpdateAsync(accountSetId, cancellationToken);
            var byId = bills.Where(b => request.BillIds.Contains(b.Id)).ToDictionary(b => b.Id, b => b);

            var generated = 0;
            var skipped = 0;
            var failures = new List<ExpenseVoucherFailureDto>();
            foreach (var id in request.BillIds.Distinct())
            {
                if (!byId.TryGetValue(id, out var bill))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    await GenerateForBillAsync(accountSetId, bill, new GenerateExpenseVoucherRequest
                    {
                        ExpenseAccountId = request.ExpenseAccountId,
                        CounterAccountId = request.CounterAccountId,
                        TaxAccountId = request.TaxAccountId,
                        Word = request.Word,
                        Preparer = request.Preparer,
                    }, cancellationToken);
                    generated++;
                }
                catch (ExpenseException ex)
                {
                    failures.Add(new ExpenseVoucherFailureDto { BillId = bill.Id, BillNo = bill.BillNo, Reason = ex.Message });
                }
            }

            if (generated > 0)
            {
                await _expenseRepository.SaveChangesAsync(cancellationToken);
            }

            return new ExpenseVoucherBatchResultDto { Generated = generated, Skipped = skipped, Failures = failures };
        }

        /// <summary>单张生成凭证的核心逻辑（不提交工作单元）。</summary>
        private async Task<string> GenerateForBillAsync(Guid accountSetId, ExpenseBill bill, GenerateExpenseVoucherRequest request, CancellationToken cancellationToken)
        {
            if (bill.Status == ExpenseBillStatus.Posted)
            {
                throw new ExpenseException($"单据「{bill.BillNo}」已生成凭证，请勿重复生成。");
            }

            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var expense = RequireAccount(accountsById, request.ExpenseAccountId, "费用科目");
            var counter = RequireAccount(accountsById, request.CounterAccountId, "对方科目");
            if (expense.Id == counter.Id)
            {
                throw new ExpenseException("费用科目与对方科目不能相同。");
            }

            var entries = new List<CreateVoucherEntryRequest>();
            var net = bill.Amount - bill.DeductibleTax;
            if (net > 0m)
            {
                entries.Add(new CreateVoucherEntryRequest
                {
                    Summary = bill.Reason,
                    AccountId = expense.Id,
                    Debit = net,
                    Credit = 0m,
                });
            }

            if (bill.DeductibleTax > 0m)
            {
                var tax = await ResolveTaxAccountAsync(accountSetId, request.TaxAccountId, accountsById, cancellationToken);
                entries.Add(new CreateVoucherEntryRequest
                {
                    Summary = bill.Reason,
                    AccountId = tax.Id,
                    Debit = bill.DeductibleTax,
                    Credit = 0m,
                });
            }

            entries.Add(new CreateVoucherEntryRequest
            {
                Summary = bill.Reason,
                AccountId = counter.Id,
                Debit = 0m,
                Credit = bill.Amount,
            });

            var voucher = await _voucherService.CreateAsync(
                accountSetId,
                string.IsNullOrWhiteSpace(request.Preparer) ? "系统" : request.Preparer,
                new CreateVoucherRequest
                {
                    Word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim(),
                    Number = 0,
                    Date = bill.BillDate,
                    AttachmentCount = 0,
                    Entries = entries,
                },
                cancellationToken);

            bill.MarkPosted(voucher.Id);
            return $"{voucher.Word}-{voucher.Number}";
        }

        /// <summary>进项税额科目：显式指定优先，否则取 2221 下名称含「进项税额」的末级科目。</summary>
        private async Task<Account> ResolveTaxAccountAsync(
            Guid accountSetId,
            Guid? explicitId,
            IReadOnlyDictionary<Guid, Account> accountsById,
            CancellationToken cancellationToken)
        {
            if (explicitId is { } id && id != Guid.Empty)
            {
                return RequireAccount(accountsById, id, "进项税额科目");
            }

            var candidate = accountsById.Values
                .Where(a => a.IsLeaf && a.IsEnabled && a.Code.StartsWith("2221", StringComparison.Ordinal) && a.Name.Contains("进项税额", StringComparison.Ordinal))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is null)
            {
                candidate = accountsById.Values
                    .Where(a => a.IsLeaf && a.IsEnabled && a.Code.StartsWith("2221", StringComparison.Ordinal))
                    .OrderBy(a => a.Code, StringComparer.Ordinal)
                    .FirstOrDefault();
            }

            if (candidate is null)
            {
                throw new ExpenseException("未找到「应交税费-进项税额」科目（2221 开头），请在生成凭证时手动指定或先在科目设置中新增。");
            }

            await Task.CompletedTask;
            _ = accountSetId;
            return candidate;
        }

        // —— 导入导出 ——

        /// <summary>导出全部费用单据。</summary>
        public async Task<byte[]> ExportBillsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var bills = await GetBillsAsync(accountSetId, cancellationToken);
            return ExpenseExportBuilder.BuildBills(bills);
        }

        /// <summary>费用单据导入模板。</summary>
        public byte[] BuildImportTemplate() => ExpenseExportBuilder.BuildTemplate();

        /// <summary>导入费用单据：按单据编号匹配，存在则更新，不存在则新建。</summary>
        public async Task<ExpenseImportResultDto> ImportBillsAsync(Guid accountSetId, IReadOnlyList<ExpenseImportRow> rows, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var existing = (await _expenseRepository.GetBillsAsync(accountSetId, cancellationToken))
                .ToDictionary(b => b.BillNo, b => b.Id, StringComparer.Ordinal);

            var failures = new List<ExpenseImportFailureDto>();
            var imported = 0;
            var updated = 0;

            foreach (var row in rows)
            {
                var billNo = row.BillNo?.Trim() ?? string.Empty;
                if (!DateOnly.TryParse(row.BillDate?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var billDate))
                {
                    failures.Add(new ExpenseImportFailureDto { Row = row.Row, Reason = "单据日期格式不正确，应为 yyyy-MM-dd。" });
                    continue;
                }

                decimal amount;
                decimal deductibleTax;
                try
                {
                    amount = ParseAmount(row.Amount);
                    deductibleTax = ParseAmount(row.DeductibleTax);
                }
                catch (FormatException)
                {
                    failures.Add(new ExpenseImportFailureDto { Row = row.Row, Reason = "金额格式不正确，应填写数字（如 1000.00）。" });
                    continue;
                }

                try
                {
                    var existingId = billNo.Length > 0 && existing.TryGetValue(billNo, out var eid) ? eid : Guid.Empty;
                    var tracked = existingId != Guid.Empty
                        ? await _expenseRepository.GetBillForUpdateAsync(accountSetId, existingId, cancellationToken)
                        : null;
                    if (tracked is null)
                    {
                        var bill = ExpenseBill.Create(
                            accountSetId,
                            billNo.Length > 0 ? billNo : null,
                            billDate,
                            row.Reason ?? string.Empty,
                            row.ExpenseType ?? string.Empty,
                            row.Payee,
                            row.SettleMethod,
                            amount,
                            deductibleTax);
                        if (billNo.Length == 0)
                        {
                            bill.AssignBillNo(await GenerateBillNoAsync(accountSetId, cancellationToken));
                        }

                        await _expenseRepository.AddBillAsync(bill, cancellationToken);
                        imported++;
                    }
                    else
                    {
                        tracked.Update(billDate, row.Reason ?? string.Empty, row.ExpenseType ?? string.Empty, row.Payee, row.SettleMethod, amount, deductibleTax);
                        updated++;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    failures.Add(new ExpenseImportFailureDto { Row = row.Row, Reason = Plain(ex) });
                }
            }

            if (imported + updated > 0)
            {
                await _expenseRepository.SaveChangesAsync(cancellationToken);
            }

            return new ExpenseImportResultDto { Imported = imported, Updated = updated, Failures = failures };
        }

        /// <summary>按「FY-序号」生成下一个可用单据编号。</summary>
        private async Task<string> GenerateBillNoAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var bills = await _expenseRepository.GetBillsAsync(accountSetId, cancellationToken);
            var used = bills.Select(b => b.BillNo).ToHashSet(StringComparer.Ordinal);
            for (var i = 1; i < 100000; i++)
            {
                var candidate = $"FY-{i:D4}";
                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new ExpenseException("自动生成单据编号失败，请手工填写单据编号。");
        }

        private static ExpenseBill BuildBill(Guid accountSetId, SaveExpenseBillRequest request)
        {
            try
            {
                return ExpenseBill.Create(
                    accountSetId,
                    request.BillNo,
                    request.BillDate,
                    request.Reason,
                    request.ExpenseType,
                    request.Payee,
                    request.SettleMethod,
                    request.Amount,
                    request.DeductibleTax);
            }
            catch (ArgumentException ex)
            {
                throw new ExpenseException(Plain(ex));
            }
        }

        /// <summary>去掉 ArgumentException 追加的「(Parameter 'xxx')」尾巴，只留业务提示。</summary>
        private static string Plain(Exception ex)
        {
            var message = ex.Message;
            var index = message.IndexOf("(Parameter", StringComparison.Ordinal);
            return index > 0 ? message[..index].TrimEnd() : message;
        }

        private static ExpenseBillDto ToDto(ExpenseBill bill, IReadOnlyDictionary<Guid, VoucherRef> vouchersById)
            => new()
            {
                Id = bill.Id,
                BillNo = bill.BillNo,
                BillDate = bill.BillDate,
                Reason = bill.Reason,
                ExpenseType = bill.ExpenseType,
                Payee = bill.Payee,
                SettleMethod = bill.SettleMethod,
                Amount = bill.Amount,
                DeductibleTax = bill.DeductibleTax,
                Status = bill.Status,
                VoucherId = bill.VoucherId,
                VoucherText = bill.VoucherId is { } id && vouchersById.TryGetValue(id, out var voucher)
                    ? $"{voucher.Word}-{voucher.Number}"
                    : null,
            };

        private static Account RequireAccount(IReadOnlyDictionary<Guid, Account> accountsById, Guid id, string roleName)
        {
            if (id == Guid.Empty || !accountsById.TryGetValue(id, out var account))
            {
                throw new ExpenseException($"所选{roleName}不存在。");
            }

            if (!account.IsLeaf)
            {
                throw new ExpenseException($"所选{roleName}「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
            }

            if (!account.IsEnabled)
            {
                throw new ExpenseException($"所选{roleName}「{account.Code} {account.Name}」已停用，不能用于记账。");
            }

            return account;
        }

        private static decimal ParseAmount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0m;
            }

            var cleaned = text.Trim().Replace(",", string.Empty).Replace("￥", string.Empty).Replace("¥", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException();
        }

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new ExpenseException("指定的账套不存在。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsByIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
