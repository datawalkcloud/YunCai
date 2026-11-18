using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YunCai.Modules.Tax.Application;

namespace YunCai.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 把个税「查询统计 → 缴税记录查询」取回的 JSON 解析成缴税记录行。
    ///
    /// 这一页的报文被瑞数改写成密文，数据来自 <c>TaxPortalPageScripts.IitTable</c> 抓的 el-table 或
    /// 页面解密后的明文钩子，键就是税局展示的中文列名，所以沿用提名人册那套「候选列名取值」。
    /// 锚是「缴税日期 + 电子税票号/缴税金额」：申报记录表没有缴税日期，不会被混进来。
    /// 页面的日期框不保证填得进去，所以解析后仍按所选区间过滤，避免把默认区间的数据当成用户要的那段。
    /// </summary>
    internal static class IitPaymentPayloadParser
    {
        private const int MaxRows = 2000;

        private static readonly string[] PeriodHints =
            ["税款所属期", "税款所属月份", "税款所属时期", "所属期", "税款所属期起止", "ssqs", "skssq", "period"];

        private static readonly string[] FormHints =
            ["报表类型", "申报表", "报表名称", "申报表名称", "所报报表", "bblx", "bbmc"];

        private static readonly string[] TicketHints =
            ["电子税票号", "电子税票号码", "电子缴款书号", "缴款书号", "税票号码", "票证号码", "凭证号码",
             "dzsphm", "dzsph", "pzhm", "pzh", "ypzh", "voucherNo"];

        private static readonly string[] AmountHints =
            ["缴税金额", "缴款金额", "实缴（退）金额", "实缴(退)金额", "实缴金额", "实缴税额", "入库金额", "金额",
             "sjje", "jkje", "se"];

        private static readonly string[] PaidOnHints =
            ["缴税日期", "缴款日期", "入库日期", "入（退）库日期", "入(退)库日期", "解缴日期", "缴款时间", "入库时间",
             "jkrq", "rkrq", "jkrqStr", "rkrqStr"];

        private static readonly string[] TaxKindHints =
            ["缴税种类", "税款种类", "税费种类", "种类", "正税滞纳金罚款", "skzl", "skzlMc", "skzl_mc"];

        private static readonly string[] MethodHints =
            ["缴税方式", "税费缴纳方式", "缴款方式", "缴纳方式", "缴费方式", "税款缴纳方式",
             "jkfs", "jkfsMc", "skjnfs", "skjnfsMc"];

        /// <summary>
        /// 解析报文并按所选区间过滤。byPeriod=true 时按税款所属期（yyyy-MM 或 yyyy年M月）比，
        /// 否则按缴税日期比；日期解析不出的行一律保留——宁可多展示一行，也不让会计以为没缴过。
        /// </summary>
        public static IReadOnlyList<TaxIitPaymentRowDto> Parse(
            IReadOnlyList<CapturedJsonPayload> payloads,
            bool byPeriod,
            string? from,
            string? to,
            out IReadOnlyList<string> diagnostics)
        {
            var rows = new List<TaxIitPaymentRowDto>();
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

            var kept = IitPayloadReader.DedupBy(rows, row => row.Key)
                .Where(row => WithinRange(row, byPeriod, from, to))
                .ToList();

            if (kept.Count > 0)
            {
                // 税局分段/翻页取回时顺序不定，统一按缴税日期倒序，最近一笔排在最前。
                kept.Sort((left, right) => string.CompareOrdinal(
                    byPeriod ? right.Period : right.PaidOn,
                    byPeriod ? left.Period : left.PaidOn));
            }

            if (rows.Count > 0 && kept.Count == 0)
            {
                notes.Add($"解析到 {rows.Count} 条缴税记录，日期都不在所选区间内。");
            }

            diagnostics = kept.Count > 0 ? Array.Empty<string>() : notes.Take(20).ToList();
            return kept;
        }

        private static List<TaxIitPaymentRowDto> ParseArray(JsonArray array)
        {
            var rows = new List<TaxIitPaymentRowDto>();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var paidOn = IitPayloadReader.PickDate(obj, PaidOnHints);
                var ticketNo = IitPayloadReader.PickText(obj, TicketHints);
                var amount = IitPayloadReader.PickText(obj, AmountHints);
                // 缴税日期 + 票号/金额才是一条缴税记录；申报记录表、菜单、合计行都没有这组锚字段。
                if (paidOn.Length == 0 || (ticketNo.Length == 0 && amount.Length == 0))
                {
                    continue;
                }

                var period = IitPayloadReader.PickText(obj, PeriodHints);
                var form = IitPayloadReader.PickText(obj, FormHints);
                rows.Add(new TaxIitPaymentRowDto
                {
                    Key = $"{period}|{form}|{ticketNo}|{paidOn}|{amount}",
                    Period = period,
                    FormName = form,
                    ETicketNo = ticketNo,
                    Amount = amount,
                    PaidOn = paidOn,
                    TaxKind = IitPayloadReader.PickText(obj, TaxKindHints),
                    Method = IitPayloadReader.PickText(obj, MethodHints),
                    Raw = obj.ToJsonString(IitPayloadReader.RawJsonOptions),
                });
                if (rows.Count >= MaxRows)
                {
                    break;
                }
            }

            return rows;
        }

        private static bool WithinRange(TaxIitPaymentRowDto row, bool byPeriod, string? from, string? to)
        {
            if (byPeriod)
            {
                var key = MonthKey(row.Period);
                if (key.Length == 0)
                {
                    return true;
                }

                var fromKey = MonthKey(from);
                var toKey = MonthKey(to);
                return (fromKey.Length == 0 || string.CompareOrdinal(key, fromKey) >= 0)
                    && (toKey.Length == 0 || string.CompareOrdinal(key, toKey) <= 0);
            }

            if (row.PaidOn.Length < 10
                || !DateOnly.TryParseExact(row.PaidOn[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var paid))
            {
                return true;
            }

            var start = ParseDay(from);
            var end = ParseDay(to);
            return (start is null || paid >= start) && (end is null || paid <= end);
        }

        private static DateOnly? ParseDay(string? text)
        {
            if (text is { Length: >= 10 }
                && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day))
            {
                return day;
            }

            return null;
        }

        /// <summary>把「2026-09」「2026年9月」「2026-09-01 至 2026-09-30」统一成 yyyy-MM。</summary>
        private static string MonthKey(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = Regex.Match(text, @"(\d{4})\D{0,3}(\d{1,2})");
            if (!match.Success
                || !int.TryParse(match.Groups[1].Value, out var year)
                || !int.TryParse(match.Groups[2].Value, out var month)
                || month is < 1 or > 12)
            {
                return string.Empty;
            }

            return $"{year:D4}-{month:D2}";
        }
    }
}
