using YunCai.Modules.Ledgers.Application;
using YunCai.Modules.Reports.Domain;

namespace YunCai.Modules.Reports.Application
{

    /// <summary>
    /// 科目余额表应用服务：按选定期间从账簿数据集合投影各科目的期初、本期发生额与期末余额。
    /// <para>
    /// 口径（设计文档 §0）：只统计**已过账**凭证；查询区间归一到自然月；内部一律以「借 - 贷」净额累计，
    /// 仅在输出层按正负拆回借/贷两栏。期初 = 科目录入的启用期初余额 + 期间起始日之前已过账分录的净额，
    /// 期末 = 期初 + 本期净额。因每张凭证借贷平衡且期初经试算平衡校验，合计恒满足试算平衡。
    /// </para>
    /// <para>
    /// 输出按科目层级铺开：有余额或发生额的科目连同其全部上级一并列出，上级行金额为整棵子树合计
    /// （与总账 <see cref="LedgerDataSet.GetAccountBalance"/> 同口径）；<see cref="TrialBalanceRowDto.Level"/>
    /// 与 <see cref="TrialBalanceRowDto.ParentCode"/> 供前端挂成树。合计只累加一级科目，避免上下级重复。
    /// </para>
    /// </summary>
    public sealed class TrialBalanceService
    {
        private readonly LedgerDataSetFactory _ledgerDataSetFactory;

        public TrialBalanceService(LedgerDataSetFactory ledgerDataSetFactory)
        {
            _ledgerDataSetFactory = ledgerDataSetFactory;
        }

        /// <summary>
        /// 生成指定账套、指定日期范围的科目余额表。等价于带日期的 <see cref="GetAsync(Guid, LedgerQueryOptions, CancellationToken)"/>
        /// 重载，仅按日期装载、不做其他显示裁剪，保持既有调用与单测不变。
        /// </summary>
        public Task<TrialBalanceDto> GetAsync(Guid accountSetId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
            => GetAsync(accountSetId, new LedgerQueryOptions { StartDate = startDate, EndDate = endDate }, cancellationToken);

        /// <summary>
        /// 生成指定账套、指定查询条件的科目余额表。startDate/endDate 省略时默认取全部已过账凭证所在月。
        /// <para>
        /// 装载阶段只按「日期区间 + 已过账」裁剪（见 <see cref="LedgerDataSet"/> 的设计约定），科目级次、
        /// 起止科目、余额/发生额过滤等「显示条件」一律在投影成行之后再做，这样上级行的合计口径不受显示条件影响，
        /// 被裁掉的明细若仍挂着某上级，该上级会一并保留以维持树形。
        /// </para>
        /// </summary>
        public async Task<TrialBalanceDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            // 装载只传日期：保全全部科目与完整子树合计，级次/编码过滤放到行级别处理。
            var dataSet = await _ledgerDataSetFactory.LoadAsync(
                accountSetId,
                new LedgerQueryOptions { StartDate = options.StartDate, EndDate = options.EndDate },
                cancellationToken);

            // 先找出自身有余额或发生额的科目，再沿上级链补齐祖先——上级行要有下级合计才成树，
            // 否则明细科目在表上会凭空出现、看不出挂在哪个一级科目下。
            var visible = new HashSet<Guid>();
            foreach (var pair in dataSet.Balances.ByAccount)
            {
                if (pair.Value.IsEmpty)
                {
                    continue;
                }

                var current = dataSet.AccountsById.GetValueOrDefault(pair.Key);
                while (current is not null && visible.Add(current.Id))
                {
                    current = current.ParentId is { } parentId
                        ? dataSet.AccountsById.GetValueOrDefault(parentId)
                        : null;
                }
            }

            var allRows = new List<TrialBalanceRowDto>(visible.Count);
            foreach (var accountId in visible)
            {
                var account = dataSet.AccountsById[accountId];
                // 上级科目取整棵子树合计，与总账 GetAccountBalance 同一口径：上级行恒等于下级之和。
                var balance = dataSet.GetAccountBalance(account);
                var (openingDebit, openingCredit) = BalanceAmounts.SplitBySign(balance.OpeningNet);
                var (endingDebit, endingCredit) = BalanceAmounts.SplitBySign(balance.EndingNet);

                allRows.Add(new TrialBalanceRowDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    Level = account.Level,
                    ParentCode = account.ParentId is { } parentId
                        ? dataSet.AccountsById[parentId].Code
                        : null,
                    OpeningDebit = openingDebit,
                    OpeningCredit = openingCredit,
                    PeriodDebit = balance.PeriodDebit,
                    PeriodCredit = balance.PeriodCredit,
                    EndingDebit = endingDebit,
                    EndingCredit = endingCredit,
                });
            }

            // 按显示条件裁剪行：科目级次、起止科目编码、余额为零 / 无发生额。
            // 被裁掉的明细若仍挂在某个上级下，该上级（及其祖先）一并保留，否则前端挂不成树。
            var rowsByCode = allRows.ToDictionary(r => r.Code ?? "", r => r);
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in allRows)
            {
                if (!PassesDisplayFilters(row, options))
                {
                    continue;
                }

                MarkRowWithAncestors(row.Code ?? "", rowsByCode, keep);
            }

            var rows = allRows.Where(r => keep.Contains(r.Code ?? "")).OrderBy(r => r.Code, StringComparer.Ordinal).ToList();

            return new TrialBalanceDto
            {
                StartDate = dataSet.StartDate.ToString("yyyy-MM-dd"),
                EndDate = dataSet.EndDate.ToString("yyyy-MM-dd"),
                PeriodText = dataSet.PeriodText,
                Rows = rows,
            };
        }

        /// <summary>科目余额表的行级显示裁剪：级次区间、编码区间、余额为 0、无发生额且余额为 0。</summary>
        private static bool PassesDisplayFilters(TrialBalanceRowDto row, LedgerQueryOptions options)
        {
            var level = row.Level;
            if (options.MinLevel is { } min && level < min)
            {
                return false;
            }

            if (options.MaxLevel is { } max && level > max)
            {
                return false;
            }

            var code = row.Code ?? "";
            if (!string.IsNullOrWhiteSpace(options.AccountFrom)
                && string.Compare(code, options.AccountFrom, StringComparison.Ordinal) < 0)
            {
                return false;
            }

            var upper = string.IsNullOrWhiteSpace(options.AccountTo) ? "999999" : options.AccountTo;
            if (string.Compare(code, upper, StringComparison.Ordinal) > 0)
            {
                return false;
            }

            var openingNet = row.OpeningDebit - row.OpeningCredit;
            var endingNet = row.EndingDebit - row.EndingCredit;
            if (options.HideZeroBalance && openingNet == 0 && endingNet == 0)
            {
                return false;
            }

            if (options.HideNoActivity
                && row.PeriodDebit == 0 && row.PeriodCredit == 0
                && openingNet == 0 && endingNet == 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>把行及其全部祖先标记为保留（已标记过的节点其祖先也已就位，直接截断上溯）。</summary>
        private static void MarkRowWithAncestors(string code, Dictionary<string, TrialBalanceRowDto> rowsByCode, HashSet<string> keep)
        {
            var current = code;
            while (current is not null && rowsByCode.TryGetValue(current, out var row))
            {
                if (!keep.Add(current))
                {
                    break;
                }

                current = row.ParentCode;
            }
        }
    }
}
