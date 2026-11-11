using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 利润表（会企02表）行次归集的共享口径：科目编码 → 标准行次、行方向与行名称。
    /// 供利润表、季报、部门/项目利润表等共用，避免各处复制归集规则。
    /// </summary>
    public static class IncomeLineMapper
    {
        /// <summary>收入方向的行次（贷方为正）：营业收入、公允价值变动收益、投资收益、营业外收入。</summary>
        public static bool IsRevenueLine(int line)
        {
            return line is 1 or 8 or 9 or 12;
        }

        /// <summary>
        /// 将损益类科目按《企业会计准则》标准编码归集到会企02表行次。
        /// 行次口径与柠檬云「企业会计准则（2007 年）」版式一致（1-32 行）。
        /// 未识别的损益科目按余额方向兜底：贷方并入营业收入(1)，借方并入营业成本(2)，保证净利润完整。
        /// </summary>
        public static int MapLine(string code, BalanceDirection direction)
        {
            var p = Prefix(code);
            return p switch
            {
                "6001" or "6051" => 1,             // 营业收入（主营业务收入 + 其他业务收入）
                "6401" or "6402" => 2,             // 营业成本（主营业务成本 + 其他业务成本）
                "6403" => 3,                       // 税金及附加
                "6601" => 4,                       // 销售费用
                "6602" or "6604" => 5,             // 管理费用（2019 版单列的研发费用并入管理费用）
                "6603" => 6,                       // 财务费用
                "6701" or "6702" => 7,             // 资产减值损失（信用减值损失同归此行）
                "6101" => 8,                       // 公允价值变动收益
                "6111" => 9,                       // 投资收益
                "6301" or "6117" or "6115" or "6116" => 12, // 营业外收入（其他收益/资产处置收益并入）
                "6711" => 14,                      // 营业外支出
                "6801" or "5801" => 17,            // 所得税费用（新旧准则编码）
                _ => direction == BalanceDirection.Credit ? 1 : 2, // 兜底：按方向并入营业收入/营业成本
            };
        }

        /// <summary>「其中」子项行次（利息费用 33、政府补助 34），不参与营业利润/利润总额/净利润合计。</summary>
        public static bool IsSubItemLine(int line)
        {
            return line is 33 or 34;
        }

        /// <summary>
        /// 「其中」子项行次：利息费用（行 33）、政府补助（行 34）。子项不参与报表合计，
        /// 仅用于与准则模板的单列口径对齐。
        /// </summary>
        public static int? SubLine(Account account)
        {
            var p = Prefix(account.Code);
            if (p is "6603" or "5503" && account.Name.Contains("利息"))
            {
                return 33; // 财务费用-利息费用（收入以“-”号填列）
            }
            if (p == "6301" && account.Name.Contains("政府补助"))
            {
                return 34; // 营业外收入-其中：政府补助
            }
            return null;
        }

        /// <summary>标准行次对应的利润表项目名称（用于部门/项目利润表等按项目名输出）。</summary>
        public static string LineName(int line)
        {
            return line switch
            {
                1 => "营业收入",
                2 => "营业成本",
                3 => "税金及附加",
                4 => "销售费用",
                5 => "管理费用",
                6 => "财务费用",
                7 => "资产减值损失",
                8 => "公允价值变动收益",
                9 => "投资收益",
                12 => "营业外收入",
                14 => "营业外支出",
                17 => "所得税费用",
                33 => "其中：利息费用（收入以“-”号填列）",
                34 => "其中：政府补助",
                _ => "其他项目",
            };
        }

        /// <summary>取科目编码前 4 位作为一级科目归集键（明细科目以一级编码为前缀）。</summary>
        public static string Prefix(string code)
        {
            return code.Length <= 4 ? code : code[..4];
        }
    }
}
