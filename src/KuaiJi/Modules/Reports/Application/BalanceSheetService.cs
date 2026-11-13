using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 资产负债表（会企01表）应用服务：从凭证分录汇总各科目余额，并按《企业会计准则》标准科目编码
    /// 归集到报表标准行次。金额按行次的正常方向净额列示（含累计折旧、减值准备等备抵科目）。
    /// 未做期末结转时，损益类科目的当期净利润并入「未分配利润」，以保证「资产总计 = 负债和所有者权益总计」恒成立。
    /// </summary>
    public sealed class BalanceSheetService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public BalanceSheetService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>
        /// 生成指定账套、指定会计期间的资产负债表。year/month 省略时优先取最近有凭证的月份，
        /// 无凭证时回退账套启用期间。期末余额取期间月末，年初余额取该年 1 月 1 日。
        /// </summary>
        public async Task<BalanceSheetDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var template = BalanceSheetTemplates.Resolve(accountSet.Standard);
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            // 报表只统计已过账凭证（口径 §0.1）：反过账退回的草稿不得入账。
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1);          // 期末：不含次月
            var yearStart = new DateOnly(resolvedYear, 1, 1);   // 年初：本年 1 月 1 日之前

            // 逐科目累计净发生额：期末（截至月末）与年初（截至本年 1 月 1 日前）。
            var endingNet = new Dictionary<Guid, decimal>();
            var openingNet = new Dictionary<Guid, decimal>();

            // 科目启用期初余额先于一切凭证，同时计入期末与年初（按余额方向换算为借-贷视角）。
            foreach (var account in accounts.Where(a => a.IsLeaf && a.OpeningBalance != 0m))
            {
                var netDebit = account.Direction == BalanceDirection.Debit
                    ? account.OpeningBalance
                    : -account.OpeningBalance;
                endingNet[account.Id] = endingNet.GetValueOrDefault(account.Id) + netDebit;
                openingNet[account.Id] = openingNet.GetValueOrDefault(account.Id) + netDebit;
            }

            foreach (var voucher in vouchers)
            {
                foreach (var entry in voucher.Entries)
                {
                    var delta = entry.Debit - entry.Credit;
                    if (voucher.Date < periodEnd)
                    {
                        endingNet[entry.AccountId] = endingNet.GetValueOrDefault(entry.AccountId) + delta;
                    }

                    if (voucher.Date < yearStart)
                    {
                        openingNet[entry.AccountId] = openingNet.GetValueOrDefault(entry.AccountId) + delta;
                    }
                }
            }

            // 按报表行次累计金额（值已换算为该行的正常方向：资产借方为正，负债/权益贷方为正）。
            var endingByLine = new Dictionary<int, decimal>();
            var openingByLine = new Dictionary<int, decimal>();

            var accountIds = new HashSet<Guid>(endingNet.Keys);
            accountIds.UnionWith(openingNet.Keys);

            foreach (var accountId in accountIds)
            {
                if (!accountsById.TryGetValue(accountId, out var account))
                {
                    continue;
                }

                var endingDelta = endingNet.GetValueOrDefault(accountId); // 借-贷
                var openingDelta = openingNet.GetValueOrDefault(accountId);

                if (template == BalanceSheetTemplate.SmallBusiness)
                {
                    // 小企业版式：存货总计与明细行、固定资产原价/累计折旧分行归集。
                    foreach (var (targetLine, sign) in MapSmallBusinessLines(account.Code, account.Category))
                    {
                        endingByLine[targetLine] = endingByLine.GetValueOrDefault(targetLine) + endingDelta * sign;
                        openingByLine[targetLine] = openingByLine.GetValueOrDefault(targetLine) + openingDelta * sign;
                    }

                    continue;
                }

                var line = MapLine(account.Code, account.Category);

                // 资产/成本类以借方为正方向；负债/权益/损益类以贷方为正方向。
                var signed = account.Category is AccountCategory.Asset or AccountCategory.Cost ? 1m : -1m;
                endingByLine[line] = endingByLine.GetValueOrDefault(line) + endingDelta * signed;
                openingByLine[line] = openingByLine.GetValueOrDefault(line) + openingDelta * signed;
            }

            var lines = new List<BalanceSheetLineDto>();
            var allLines = new HashSet<int>(endingByLine.Keys);
            allLines.UnionWith(openingByLine.Keys);
            foreach (var line in allLines.OrderBy(l => l))
            {
                lines.Add(new BalanceSheetLineDto
                {
                    Line = line,
                    Ending = endingByLine.GetValueOrDefault(line),
                    Opening = openingByLine.GetValueOrDefault(line),
                });
            }

            var endingDay = periodEnd.AddDays(-1);
            return new BalanceSheetDto
            {
                Standard = (int)accountSet.Standard,
                TemplateTitle = BalanceSheetTemplates.Title(template),
                Year = resolvedYear,
                Month = resolvedMonth,
                EndingDate = $"{endingDay.Year:D4}年{endingDay.Month:D2}月{endingDay.Day:D2}日",
                OpeningDate = $"{resolvedYear:D4}年01月01日",
                Lines = lines,
            };
        }

        /// <summary>
        /// 将科目按《企业会计准则》标准编码归集到会企01表行次。损益类（含本期结转前净利润）
        /// 与未识别的权益科目并入「未分配利润」(54)；未识别的资产/负债并入「其他流动资产/负债」。
        /// 备抵科目（累计折旧、坏账准备等）与其对应科目归入同一行，靠净额自动抵减。
        /// 补充资料（间接法）等报表共用该归集口径。
        /// </summary>
        internal static int MapLine(string code, AccountCategory category)
        {
            return category switch
            {
                AccountCategory.Asset or AccountCategory.Cost => MapAssetLine(code, category),
                AccountCategory.Liability => MapLiabilityLine(code),
                AccountCategory.Equity => MapEquityLine(code),
                _ => 60, // 损益类 → 未分配利润
            };
        }

        private static int MapAssetLine(string code, AccountCategory category)
        {
            // 成本类（生产成本、制造费用、劳务成本等）余额属在产品，列入存货；
            // 研发支出余额属「开发支出」，单列行 25。
            if (category == AccountCategory.Cost)
            {
                return Prefix(code) == "5301" ? 25 : 9;
            }

            var p = Prefix(code);
            return p switch
            {
                "1001" or "1002" or "1012" => 1,   // 货币资金
                "1101" => 2,                       // 以公允价值计量且其变动计入当期损益的金融资产
                "1121" => 3,                       // 应收票据
                "1122" or "1231" => 4,             // 应收账款（含坏账准备备抵）
                "1123" => 5,                       // 预付账款
                "1132" => 6,                       // 应收利息
                "1131" => 7,                       // 应收股利
                "1221" => 8,                       // 其他应收款
                "1401" or "1402" or "1403" or "1404" or "1405" or "1406"
                    or "1407" or "1408" or "1411" or "1421" or "1471" => 9, // 存货
                "1503" => 13,                      // 可供出售金融资产
                "1501" or "1502" => 14,            // 持有至到期投资（含减值准备）
                "1531" or "1532" => 15,            // 长期应收款
                "1511" or "1512" => 16,            // 长期股权投资（含减值准备）
                "1521" => 17,                      // 投资性房地产
                "1601" or "1602" or "1603" => 18,  // 固定资产（含累计折旧、减值准备备抵）
                "1604" => 19,                      // 在建工程
                "1605" => 20,                      // 工程物资
                "1606" => 21,                      // 固定资产清理
                "1621" or "1622" or "1623" => 22,  // 生产性生物资产
                "1631" or "1632" => 23,            // 油气资产
                "1701" or "1702" or "1703" => 24,  // 无形资产（含累计摊销、减值准备）
                "1711" => 26,                      // 商誉
                "1801" => 27,                      // 长期待摊费用
                "1811" => 28,                      // 递延所得税资产
                _ => 11,                           // 其他流动资产（兜底，保证总计）
            };
        }

        private static int MapLiabilityLine(string code)
        {
            var p = Prefix(code);
            return p switch
            {
                "2001" => 32,             // 短期借款
                "2101" => 33,             // 以公允价值计量且其变动计入当期损益的金融负债
                "2201" => 34,             // 应付票据
                "2202" => 35,             // 应付账款
                "2203" => 36,             // 预收款项
                "2211" => 37,             // 应付职工薪酬
                "2221" => 38,             // 应交税费
                "2231" => 39,             // 应付利息
                "2232" => 40,             // 应付股利
                "2241" => 41,             // 其他应付款
                "2501" => 45,             // 长期借款
                "2502" => 46,             // 应付债券
                "2701" => 47,             // 长期应付款
                "2711" => 48,             // 专项应付款
                "2801" => 49,             // 预计负债
                "2401" => 50,             // 递延收益
                "2901" => 51,             // 递延所得税负债
                _ => 43,                  // 其他流动负债（兜底）
            };
        }

        private static int MapEquityLine(string code)
        {
            var p = Prefix(code);
            return p switch
            {
                // 新准则（4xxx 权益类）
                "4001" => 55,             // 实收资本（或股本）
                "4002" or "4105" => 56,   // 资本公积（专项储备并入）
                "4201" => 57,             // 减:库存股
                "4004" => 58,             // 其他综合收益
                "4101" or "4102" => 59,   // 盈余公积（一般风险准备并入）
                "4103" or "4104" => 60,   // 未分配利润（本年利润 + 利润分配）
                // 旧准则兼容（3xxx 权益类），避免历史数据遗漏
                "3001" => 55,             // 实收资本（旧准则）
                "3002" => 56,             // 资本公积（旧准则）
                "3101" => 59,             // 盈余公积（旧准则）
                "3103" or "3104" => 60,   // 本年利润 / 利润分配（旧准则）
                _ => 60,                  // 未识别权益并入未分配利润（兜底）
            };
        }

        /// <summary>标准行次对应的项目名称（导出与展示共用；未识别行返回空）。</summary>
        public static string LineName(int line)
        {
            return line switch
            {
                1 => "货币资金",
                2 => "以公允价值计量且其变动计入当期损益的金融资产",
                3 => "应收票据",
                4 => "应收账款",
                5 => "预付账款",
                6 => "应收利息",
                7 => "应收股利",
                8 => "其他应收款",
                9 => "存货",
                10 => "一年内到期的非流动资产",
                11 => "其他流动资产",
                12 => "流动资产合计",
                13 => "可供出售金融资产",
                14 => "持有至到期投资",
                15 => "长期应收款",
                16 => "长期股权投资",
                17 => "投资性房地产",
                18 => "固定资产",
                19 => "在建工程",
                20 => "工程物资",
                21 => "固定资产清理",
                22 => "生产性生物资产",
                23 => "油气资产",
                24 => "无形资产",
                25 => "开发支出",
                26 => "商誉",
                27 => "长期待摊费用",
                28 => "递延所得税资产",
                29 => "其他非流动资产",
                30 => "非流动资产合计",
                31 => "资产总计",
                32 => "短期借款",
                33 => "以公允价值计量且其变动计入当期损益的金融负债",
                34 => "应付票据",
                35 => "应付账款",
                36 => "预收款项",
                37 => "应付职工薪酬",
                38 => "应交税费",
                39 => "应付利息",
                40 => "应付股利",
                41 => "其他应付款",
                42 => "一年内到期的非流动负债",
                43 => "其他流动负债",
                44 => "流动负债合计",
                45 => "长期借款",
                46 => "应付债券",
                47 => "长期应付款",
                48 => "专项应付款",
                49 => "预计负债",
                50 => "递延收益",
                51 => "递延所得税负债",
                52 => "其他非流动负债",
                53 => "非流动负债合计",
                54 => "负债合计",
                55 => "实收资本（或股本）",
                56 => "资本公积",
                57 => "减:库存股",
                58 => "其他综合收益",
                59 => "盈余公积",
                60 => "未分配利润",
                61 => "所有者权益（或股东权益）合计",
                62 => "负债和所有者权益（或股东权益）总计",
                _ => $"行次{line}",
            };
        }

        /// <summary>
        /// 小企业会计准则（会小企01表）行次归集。每个科目返回一个或多个「行次 + 方向符号」：
        /// 存货类同时计入主行 9 与明细行 10-13；固定资产原价、累计折旧分别落 18、19 两行。
        /// </summary>
        internal static IReadOnlyList<(int Line, decimal Sign)> MapSmallBusinessLines(string code, AccountCategory category)
        {
            var p = Prefix(code);
            switch (category)
            {
                case AccountCategory.Asset:
                    var assetLine = MapSmallBusinessAssetLine(p);
                    // 累计折旧、固定资产减值准备是备抵科目，贷方余额在「减：累计折旧」行以正数展示。
                    var sign = p is "1602" or "1603" ? -1m : 1m;
                    if (assetLine == 9)
                    {
                        var detail = MapSmallBusinessInventoryDetail(p);
                        return detail is null ? [(9, sign)] : [(9, sign), (detail.Value, sign)];
                    }

                    return [(assetLine, sign)];

                case AccountCategory.Cost:
                    // 生产成本、制造费用等余额属在产品：计入存货总额，并列入「在产品」明细行。
                    // 研发支出（5301）属「开发支出」，单列非流动资产行 26，不并入存货。
                    return p == "5301" ? [(26, 1m)] : [(9, 1m), (11, 1m)];

                case AccountCategory.Liability:
                    return [(MapSmallBusinessLiabilityLine(p), -1m)];

                case AccountCategory.Equity:
                    return [(MapSmallBusinessEquityLine(p), -1m)];

                default:
                    return [(51, -1m)]; // 损益类 → 未分配利润
            }
        }

        private static int MapSmallBusinessAssetLine(string p) => p switch
        {
            "1001" or "1002" or "1012" => 1,   // 货币资金
            "1101" => 2,                       // 短期投资
            "1121" => 3,                       // 应收票据
            "1122" or "1231" => 4,             // 应收账款（含坏账准备备抵）
            "1123" => 5,                       // 预付账款
            "1131" => 6,                       // 应收股利
            "1132" => 7,                       // 应收利息
            "1221" => 8,                       // 其他应收款
            "1401" or "1402" or "1403" or "1404" or "1405" or "1406"
                or "1407" or "1408" or "1411" or "1421" or "1471" => 9, // 存货
            "1501" or "1502" or "1503" => 16,  // 长期债券投资
            "1511" or "1512" => 17,            // 长期股权投资
            "1601" => 18,                      // 固定资产原价
            "1602" or "1603" => 19,            // 减：累计折旧（含固定资产减值准备）
            "1604" => 21,                      // 在建工程
            "1605" => 22,                      // 工程物资
            "1606" => 23,                      // 固定资产清理
            "1621" or "1622" or "1623" => 24,  // 生产性生物资产
            "1701" or "1702" or "1703" => 25,  // 无形资产
            "1801" => 27,                      // 长期待摊费用
            "1711" or "1811" or "1521" or "1531" or "1532" => 28, // 其他非流动资产
            _ => 14,                           // 其他流动资产（兜底，保证资产侧合计）
        };

        private static int? MapSmallBusinessInventoryDetail(string p) => p switch
        {
            "1403" => 10, // 原材料
            "1405" => 12, // 库存商品
            "1411" => 13, // 周转材料
            _ => null,
        };

        private static int MapSmallBusinessLiabilityLine(string p) => p switch
        {
            "2001" => 31,
            "2201" => 32,
            "2202" => 33,
            "2203" => 34,
            "2211" => 35,
            "2221" => 36,
            "2231" => 37,
            "2232" => 38,
            "2241" => 39,
            "2101" => 40,
            "2501" => 42,
            "2502" or "2701" or "2711" => 43,
            "2401" => 44,
            "2801" or "2901" => 45,
            _ => 40, // 其他流动负债（兜底）
        };

        private static int MapSmallBusinessEquityLine(string p) => p switch
        {
            "4001" or "3001" => 48, // 实收资本
            "4002" or "4003" or "4004" or "4005" or "3002" or "4105" => 49, // 资本公积
            "4101" or "4102" or "3101" => 50, // 盈余公积
            "4103" or "4104" or "3103" or "3104" => 51, // 未分配利润
            _ => 51, // 未识别权益（含库存股）并入未分配利润
        };

        /// <summary>取科目编码前 4 位作为一级科目归集键（明细科目以一级编码为前缀）。</summary>
        private static string Prefix(string code)
        {
            return code.Length <= 4 ? code : code[..4];
        }
    }
}
