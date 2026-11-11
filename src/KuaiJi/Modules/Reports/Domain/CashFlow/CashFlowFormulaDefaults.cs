namespace KuaiJi.Modules.Reports.Domain.CashFlow
{
    /// <summary>
    /// 现金流量表默认取数公式：明细行用 AUTO() 沿用系统凭证归类口径，小计/净额/净增加额
    /// 由行次引用汇总，期初/期末现金余额取现金科目余额。用户可在「编辑公式」中按行覆盖。
    /// </summary>
    public static class CashFlowFormulaDefaults
    {
        private static readonly IReadOnlyDictionary<int, string> Formulas = new Dictionary<int, string>
        {
            [1] = "AUTO()",
            [2] = "AUTO()",
            [3] = "AUTO()",
            [4] = "R1+R2+R3",
            [5] = "AUTO()",
            [6] = "AUTO()",
            [7] = "AUTO()",
            [8] = "AUTO()",
            [9] = "R5+R6+R7+R8",
            [10] = "R4-R9",
            [11] = "AUTO()",
            [12] = "AUTO()",
            [13] = "AUTO()",
            [14] = "AUTO()",
            [15] = "AUTO()",
            [16] = "R11+R12+R13+R14+R15",
            [17] = "AUTO()",
            [18] = "AUTO()",
            [19] = "AUTO()",
            [20] = "AUTO()",
            [21] = "R17+R18+R19+R20",
            [22] = "R16-R21",
            [23] = "AUTO()",
            [24] = "AUTO()",
            [25] = "AUTO()",
            [26] = "R23+R24+R25",
            [27] = "AUTO()",
            [28] = "AUTO()",
            [29] = "AUTO()",
            [30] = "R27+R28+R29",
            [31] = "R26-R30",
            [32] = "0",
            [33] = "R10+R22+R31+R32",
            [34] = "QC(\"1001\")+QC(\"1002\")+QC(\"1012\")",
            [35] = "R34+R33+R32",
        };

        /// <summary>全部行次的默认公式。</summary>
        public static IReadOnlyDictionary<int, string> All => Formulas;

        /// <summary>某行的默认公式；未知行次返回 "0"。</summary>
        public static string For(int line) => Formulas.GetValueOrDefault(line, "0");
    }
}
