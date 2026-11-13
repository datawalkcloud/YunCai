using System.Text.Json.Nodes;
using YunCai.Modules.Tax.Application;

namespace YunCai.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 把个税「查询统计 → 申报记录查询」取回的 JSON（多为页面表格抓下来的「列名: 文本」）
    /// 解析成申报记录行。
    ///
    /// 这一页的报文同样被瑞数改写成密文，实际数据来自 <c>TaxPortalPageScripts.IitTable</c> 抓的
    /// el-table，键就是税局展示的中文列名；各省列名会有细微差别（括号全半角、少一列），
    /// 所以沿用人员名册的思路：按候选列名取值，解析不出时把列名回传补规则。
    /// </summary>
    internal static class IitFilingPayloadParser
    {
        private const int MaxRows = 2000;

        // 树形表由取数脚本压平成「税款所属期 / 申报表」，非树形的省份直接把列名「税款所属月份」留在行上，两种都认。
        private static readonly string[] PeriodHints =
            ["税款所属期", "税款所属月份", "税款所属时期", "所属期", "税款所属期起", "ssqs", "period"];

        private static readonly string[] FormHints =
            ["申报表", "报表名称", "申报表名称", "所报报表", "form_name", "bbs"];

        private static readonly string[] HeadcountHints = ["申报人次", "人次", "申报人数", "rcs"];

        private static readonly string[] IncomeHints = ["当期收入", "本期收入", "收入额", "收入", "sr"];

        // 杭州这一页的表头写的是「应补/退税额」，全角括号那几种是别的省份。
        private static readonly string[] TaxHints =
            ["应补（退）税额", "应补(退)税额", "应补退税额", "应补/退税额", "应补（退）税（费）额", "税额", "se"];

        private static readonly string[] DeclareTypeHints = ["申报类型", "报表类型", "sblx"];

        private static readonly string[] ChannelHints = ["申报渠道", "办税渠道", "渠道", "qd"];

        private static readonly string[] DeclareTimeHints = ["申报时间", "申报日期", "sfsj"];

        public static IReadOnlyList<TaxIitFilingRowDto> Parse(
            IReadOnlyList<CapturedJsonPayload> payloads,
            out IReadOnlyList<string> diagnostics)
        {
            var rows = new List<TaxIitFilingRowDto>();
            var notes = new List<string>();

            foreach (var payload in payloads)
            {
                var sample = string.Empty;
                foreach (var array in IitPayloadReader.ArraysOf(payload.Body))
                {
                    var parsed = ParseArray(array);
                    if (parsed.Count > 0)
                    {
                        rows.AddRange(parsed);
                    }
                    else if (sample.Length == 0 && array[0] is JsonObject first)
                    {
                        sample = string.Join("|", first.Select(pair => pair.Key).Take(14));
                    }
                }

                if (sample.Length > 0)
                {
                    notes.Add($"{IitPayloadReader.Shorten(payload.Url)} 字段={sample}");
                }
            }

            var deduped = IitPayloadReader.DedupBy(rows, row => row.Key);
            diagnostics = deduped.Count > 0 ? Array.Empty<string>() : notes.Take(20).ToList();
            return deduped;
        }

        private static List<TaxIitFilingRowDto> ParseArray(JsonArray array)
        {
            var rows = new List<TaxIitFilingRowDto>();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var period = IitPayloadReader.PickText(obj, PeriodHints);
                var form = IitPayloadReader.PickText(obj, FormHints);
                // 所属期是这张表的锚：没有所属期的行多半是别处的表格（合计行、隐藏筛选表）被抓进来。
                if (period.Length == 0 || (form.Length == 0 && IitPayloadReader.PickText(obj, TaxHints).Length == 0))
                {
                    continue;
                }

                var declareTime = IitPayloadReader.PickText(obj, DeclareTimeHints);
                rows.Add(new TaxIitFilingRowDto
                {
                    Key = $"{period}|{form}|{declareTime}",
                    Period = period,
                    FormName = form,
                    Headcount = IitPayloadReader.PickText(obj, HeadcountHints),
                    Income = IitPayloadReader.PickText(obj, IncomeHints),
                    TaxDue = IitPayloadReader.PickText(obj, TaxHints),
                    DeclareType = IitPayloadReader.PickText(obj, DeclareTypeHints),
                    Channel = IitPayloadReader.PickText(obj, ChannelHints),
                    DeclareTime = declareTime,
                    Raw = obj.ToJsonString(IitPayloadReader.RawJsonOptions),
                });
                if (rows.Count >= MaxRows)
                {
                    break;
                }
            }

            return rows;
        }
    }
}
