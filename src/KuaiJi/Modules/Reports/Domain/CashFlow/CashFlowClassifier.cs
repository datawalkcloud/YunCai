using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Reports.Domain.CashFlow
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

        /// <summary>投资活动涉及的资产类一级编码（短期投资/长期投资/固定资产/在建工程/无形资产/长期待摊等，兼容新旧准则）。</summary>
        private static readonly IReadOnlySet<string> InvestingPrefixes = new HashSet<string>
        {
            "1101", "1102", "1401", "1402", "1403", "1404", "1405", "1406",
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
                    if (account.Name.Contains("税"))
                    {
                        return isCashInflow ? 2 : 7;   // 税费返还 / 支付各项税费
                    }
                    if (account.Name.Contains("工资") || account.Name.Contains("薪酬") || account.Name.Contains("福利"))
                    {
                        return isCashInflow ? 3 : 6;   // 收到其他经营 / 支付职工
                    }
                    // 应付账款/票据、预收、其他应付等往来：流入=销售收现，流出=购买商品
                    return isCashInflow ? 1 : 5;

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

        private static string Prefix(string code)
        {
            return code.Length <= 4 ? code : code[..4];
        }
    }
}
