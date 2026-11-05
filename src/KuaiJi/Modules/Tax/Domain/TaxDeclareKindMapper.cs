using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.Tax.Domain
{
    /// <summary>
    /// 推导申报种类要用的按户信息：纳税人资格决定增值税用哪张主表，会计制度决定财务报表申报表的名字。
    /// </summary>
    public readonly record struct TaxKindProfile(TaxpayerType TaxpayerType, AccountingStandard Standard);

    /// <summary>
    /// 税费种认定行 → 申报种类（申报表名称）。
    ///
    /// 税局的认定报文里没有「申报种类」这一列（浙江 DescribeSfzrdxx 只回征收项目、征收品目、
    /// 申报期限与认定有效期），但每行挂在哪张申报表上是由「征收项目 + 申报期限 + 按户资格」固定的，
    /// 所以在这里补出来：附加税费不单独出表，要能看出它并进增值税主表一起申报。
    /// </summary>
    public static class TaxDeclareKindMapper
    {
        /// <summary>与增值税合并申报的附加税费，申报种类跟增值税一致。</summary>
        private static readonly string[] VatSurcharges = ["城市维护建设税", "教育费附加", "地方教育附加"];

        public static string Map(string project, string period, string option, TaxKindProfile profile)
        {
            var name = (project ?? "").Trim();
            if (name.Length == 0)
            {
                return "";
            }

            var term = (period ?? "").Trim();

            if (name == "增值税" || Array.IndexOf(VatSurcharges, name) >= 0)
            {
                return profile.TaxpayerType == TaxpayerType.General
                    ? "增值税及附加税费申报（一般纳税人）"
                    : "增值税及附加税费申报（小规模纳税人）";
            }

            switch (name)
            {
                case "消费税":
                    return "消费税及附加税费申报表";
                case "企业所得税":
                    return term == "年"
                        ? "居民企业（查账征收）企业所得税年度申报表"
                        : "居民企业（查账征收）企业所得税月（季）度申报表";
                case "个人所得税":
                    return DeclareOptionKind(option) ?? "个人所得税（综合所得）";
                case "残疾人就业保障金":
                    return "残疾人就业保障金申报表";
                case "印花税":
                    return WithTerm("印花税纳税申报表", term);
                case "财务报表":
                    // 税局把财务报表按会计制度备案推送，项目名重复制度名时不写两遍。
                    return $"财务报表（{StandardName(profile.Standard)}）_{(term == "年" ? "年报" : "季报")}";
                default:
                    return name.EndsWith('税') ? $"{name}纳税申报表" : $"{name}申报表";
            }
        }

        /// <summary>个税的申报表按申报选项分：代扣代缴 / 代收代缴，其余按综合所得。</summary>
        private static string? DeclareOptionKind(string? option)
        {
            var text = (option ?? "").Trim();
            if (text.Contains("代收代缴", StringComparison.Ordinal))
            {
                return "个人所得税（代收代缴）";
            }

            return text.Contains("代扣代缴", StringComparison.Ordinal) ? "个人所得税（代扣代缴）" : null;
        }

        private static string WithTerm(string form, string term)
        {
            return term.Length == 0 ? form : $"{form}（{term}）";
        }

        private static string StandardName(AccountingStandard standard)
        {
            return standard switch
            {
                AccountingStandard.Enterprise => "企业会计准则",
                AccountingStandard.NonProfit => "民间非营利组织会计制度",
                _ => "小企业会计准则",
            };
        }
    }
}
