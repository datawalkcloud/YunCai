using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 利润表（会企02表）行次归集的共享口径：科目编码 → 标准行次、行方向与行名称。
    /// 供利润表、季报、部门/项目利润表等共用，避免各处复制归集规则。
    /// </summary>
    public static class IncomeLineMapper
    {
        /// <summary>收入方向的行次（贷方为正）：营业收入、其他收益、投资收益、公允价值变动收益、资产处置收益、营业外收入。</summary>
        public static bool IsRevenueLine(int line)
        {
            return line is 1 or 8 or 9 or 11 or 14 or 16;
        }

        /// <summary>
        /// 将损益类科目按《企业会计准则》标准编码归集到会企02表行次。
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
                "6602" => 5,                       // 管理费用
                "6604" => 6,                       // 研发费用（企业单设时）
                "6603" => 7,                       // 财务费用
                "6117" => 8,                       // 其他收益
                "6111" => 9,                       // 投资收益
                "6101" => 11,                      // 公允价值变动收益
                "6702" => 12,                      // 信用减值损失
                "6701" => 13,                      // 资产减值损失
                "6115" or "6116" => 14,            // 资产处置收益
                "6301" => 16,                      // 营业外收入
                "6711" => 17,                      // 营业外支出
                "6801" => 19,                      // 所得税费用
                _ => direction == BalanceDirection.Credit ? 1 : 2, // 兜底：按方向并入营业收入/营业成本
            };
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
                6 => "研发费用",
                7 => "财务费用",
                8 => "其他收益",
                9 => "投资收益",
                11 => "公允价值变动收益",
                12 => "信用减值损失",
                13 => "资产减值损失",
                14 => "资产处置收益",
                16 => "营业外收入",
                17 => "营业外支出",
                19 => "所得税费用",
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
