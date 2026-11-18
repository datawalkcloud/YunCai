using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Reports.Domain.CashFlow
{
    /// <summary>
    /// 现金流量归类领域服务：把现金收付的对手方科目归类到标准现金流量行次。
    /// <para>
    /// 归类口径（直接法工作底稿的简化规则）：
    /// ① 权益类对手方 → 筹资活动（出资/资本公积=吸收投资，利润分配/盈余公积=分配股利利润）；
    /// ② 负债类对手方按科目名称区分借款/税费/职工薪酬，往来类并入经营（销售收现/购买商品）；
    /// ③ 资产类对手方中，长期资产与投资类科目归入投资活动，其余（应收预付存货等）归入经营；
    /// ④ 成本类 → 经营（购买商品）；损益类按余额方向：收入类=销售收现，费用类按
    ///    薪酬/税费/成本/其他细分到对应经营行次。
    /// 科目编码同时兼容新旧准则（旧 3xxx 权益、4xxx 成本；新 4xxx 权益、5xxx 成本）。
    /// </para>
    /// </summary>
    public static class CashFlowClassifier
    {
        /// <summary>现金及现金等价物科目的一级编码（库存现金/银行存款/其他货币资金，新旧准则一致）。</summary>
        public static readonly IReadOnlySet<string> CashAccountPrefixes = new HashSet<string> { "1001", "1002", "1012" };

        /// <summary>投资活动涉及的资产类一级编码（短期投资/长期投资/固定资产/在建工程/无形资产/长期待摊等，兼容新旧准则）。
        /// 1401-1406 是存货类（材料采购/在途/原材料/库存商品等），归经营活动购买商品，不能列入投资。</summary>
        private static readonly IReadOnlySet<string> InvestingPrefixes = new HashSet<string>
        {
            "1101", "1102",
            "1501", "1503", "1511", "1512",
            "1601", "1602", "1603", "1604", "1605", "1606",
            "1701", "1702", "1703", "1711",
            "1801", "1811",
        };

        /// <summary>借款类负债的一级编码（新旧准则：短期借款/长期借款/应付债券/长期应付款）。</summary>
        private static readonly IReadOnlySet<string> LoanPrefixes = new HashSet<string> { "2001", "2101", "2301", "2311", "2321", "2501", "2502", "2701" };

        /// <summary>权益类（出资/资本公积）一级编码。</summary>
        private static readonly IReadOnlySet<string> PaidInPrefixes = new HashSet<string> { "3001", "3002", "4001", "4002" };

        /// <summary>利润分配/本年利润相关权益编码。</summary>
        private static readonly IReadOnlySet<string> ProfitDistributePrefixes = new HashSet<string> { "3101", "3103", "3104", "4101", "4103", "4104" };

        /// <summary>
        /// 归类对手方科目到现金流量标准行次。
        /// </summary>
        /// <param name="account">对手方科目。</param>
        /// <param name="isCashInflow">true=现金流入（现金科目为借方），false=现金流出。</param>
        public static int MapLine(Account account, bool isCashInflow)
        {
            var p = Prefix(account.Code);

            switch (account.Category)
            {
                case AccountCategory.Equity:
                    if (PaidInPrefixes.Contains(p))
                    {
                        return isCashInflow ? 23 : 29; // 吸收投资 / 支付其他筹资
                    }
                    if (ProfitDistributePrefixes.Contains(p))
                    {
                        return isCashInflow ? 25 : 28; // 收到其他筹资 / 分配股利利润或偿付利息
                    }
                    return isCashInflow ? 25 : 29;

                case AccountCategory.Liability:
                    if (LoanPrefixes.Contains(p) || account.Name.Contains("借款"))
                    {
                        return isCashInflow ? 24 : 27; // 取得借款 / 偿还债务
                    }
                    // 增值税进项/销项是货款的组成部分：销售含税收现并入行 1，采购含税付现并入行 5；
                    // 「支付的各项税费」只反映实际缴纳的税款，不能把计提的进销项拆到税费行。
                    if (IsVatTurnoverAccount(account))
                    {
                        return isCashInflow ? 1 : 5;
                    }
                    if (account.Name.Contains("税"))
                    {
                        return isCashInflow ? 2 : 7;   // 税费返还 / 支付各项税费
                    }
                    if (account.Name.Contains("工资") || account.Name.Contains("薪酬") || account.Name.Contains("福利"))
                    {
                        return isCashInflow ? 3 : 6;   // 收到其他经营 / 支付职工
                    }
                    // 预收类流入属于销售收现；应付/其他应付等往来流入属于收到其他经营，流出属于购买商品
                    if (isCashInflow)
                    {
                        return account.Name.Contains("预收") ? 1 : 3;
                    }
                    return 5;

                case AccountCategory.Asset:
                    if (InvestingPrefixes.Contains(p)
                        || account.Name.Contains("投资")
                        || account.Name.Contains("固定资产")
                        || account.Name.Contains("在建")
                        || account.Name.Contains("无形")
                        || account.Name.Contains("长期"))
                    {
                        if (isCashInflow)
                        {
                            return account.Name.Contains("投资") ? 11 : 13; // 收回投资 / 处置长期资产
                        }
                        return account.Name.Contains("投资") ? 18 : 17;     // 投资支付 / 购建长期资产
                    }
                    // 应收/预付/存货等其他资产：流入=销售收现（应收收回），流出=购买商品（预付/购货）
                    return isCashInflow ? 1 : 5;

                case AccountCategory.Cost:
                    return isCashInflow ? 3 : 5; // 成本类 → 购买商品（流出）

                case AccountCategory.ProfitAndLoss:
                default:
                    // 营业外收入/其他收益/资产处置收益及营业外支出：不属于销售收现，计入收到/支付其他经营
                    if (p is "6301" or "6117" or "6115" or "6116" or "6711")
                    {
                        return isCashInflow ? 3 : 8;
                    }
                    if (account.Direction == BalanceDirection.Credit)
                    {
                        // 收入类：流入=销售商品提供劳务收到的现金；流出=销售退回（计入购买商品冲减口径）
                        return isCashInflow ? 1 : 5;
                    }
                    if (account.Name.Contains("工资") || account.Name.Contains("薪酬") || account.Name.Contains("福利"))
                    {
                        return 6; // 支付职工
                    }
                    if (account.Name.Contains("税"))
                    {
                        return isCashInflow ? 2 : 7; // 税费返还 / 支付税费
                    }
                    if (p is "6401" or "6402" or "6403")
                    {
                        return 5; // 购买商品（营业成本/税金及附加）
                    }
                    return isCashInflow ? 3 : 8; // 其他经营活动
            }
        }

        /// <summary>
        /// 把小企业会计准则（会小企03表）的对手方科目归类到 22 行标准行次。
        /// 先按小准则独有的行次拆分——利息付现单独进「偿还借款利息」、分配利润进「分配利润」——
        /// 其余沿用一般企业归类结果再折到小准则行次（税费返还并入「收到其他经营」等）。
        /// </summary>
        public static int MapSmallBusinessLine(Account account, bool isCashInflow)
        {
            if (IsInterestAccount(account))
            {
                return isCashInflow ? 2 : 17; // 收到其他经营 / 偿还借款利息
            }

            if (IsProfitDistributionAccount(account))
            {
                return isCashInflow ? 15 : 18; // 吸收投资 / 分配利润
            }

            return CashFlowSmallBusinessRows.FromEnterpriseLine(MapLine(account, isCashInflow));
        }

        /// <summary>
        /// 增值税流转税明细：进项（含待认证/待抵扣/留抵）、销项（含待转销）随货款计入商品现金流。
        /// 实际缴纳的「已交税金」「未交增值税」等不属于这里，仍走税费行。
        /// </summary>
        private static bool IsVatTurnoverAccount(Account account)
        {
            if (Prefix(account.Code) != "2221")
            {
                return false;
            }

            return account.Name.Contains("进项")
                   || account.Name.Contains("销项")
                   || account.Name.Contains("待认证")
                   || account.Name.Contains("待抵扣")
                   || account.Name.Contains("待转销")
                   || account.Name.Contains("留抵");
        }

        /// <summary>利息类科目：财务费用（新 6603 / 旧 5503）与应付利息（2231），名称含「利息」的损益/负债科目同样命中。</summary>
        private static bool IsInterestAccount(Account account)
        {
            var p = Prefix(account.Code);
            return account.Category switch
            {
                AccountCategory.ProfitAndLoss => p is "6603" or "5503" || account.Name.Contains("利息"),
                AccountCategory.Liability => p is "2231" || account.Name.Contains("利息"),
                _ => false,
            };
        }

        /// <summary>利润分配类科目：权益类的利润分配/盈余公积/本年利润，以及负债类的应付利润。</summary>
        private static bool IsProfitDistributionAccount(Account account)
        {
            var p = Prefix(account.Code);
            return account.Category switch
            {
                AccountCategory.Equity => ProfitDistributePrefixes.Contains(p)
                                           || account.Name.Contains("利润")
                                           || account.Name.Contains("盈余公积"),
                AccountCategory.Liability => account.Name.Contains("应付利润") || account.Name.Contains("股利"),
                _ => false,
            };
        }

        private static string Prefix(string code)
        {
            return code.Length <= 4 ? code : code[..4];
        }
    }
}
